using System.Net;
using System.Text;
using System.Text.Json;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Scoring;
using AlfaCredit.Infrastructure.Scoring;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

/// <summary>HTTP-клиент сервиса скоринга: формат запроса по контракту, разбор ответа ML-сервиса, повторы, таймаут и откат на правила.</summary>
public sealed class HttpScoringServiceTests
{
    // Пример ответа, который прислала команда скоринга
    private const string ExampleResponse = """
        {
          "request_id": "string",
          "model_version": "rules-0.2",
          "score": 10,
          "risk_grade": "E",
          "probability_of_default": null,
          "factors": [
            { "code": "factor_debt_load", "title": "Кредитная нагрузка", "impact": -35, "value": "нет прибыли", "client_explanation": "За последние 12 месяцев нет чистой прибыли" },
            { "code": "factor_business_age", "title": "Возраст бизнеса", "impact": -20, "value": "0 мес.", "client_explanation": "Бизнес работает недолго, это повышает риск" },
            { "code": "factor_expense_ratio", "title": "Расходы к выручке", "impact": 15, "value": "0%", "client_explanation": "Расходы заметно ниже выручки" }
          ],
          "warnings": [],
          "missing_data": []
        }
        """;

    [Fact]
    public async Task Successful_response_is_mapped_to_an_external_outcome()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, ExampleResponse)));

        var outcome = await CreateService(handler).ScoreAsync(Request(), CancellationToken.None);

        outcome.Source.ShouldBe(ScoringSource.External);
        outcome.ModelVersion.ShouldBe("rules-0.2");
        outcome.Score.ShouldBe(10);
        outcome.Grade.ShouldBe(RiskGrade.E);
        outcome.ProbabilityOfDefault.ShouldBeNull();
        outcome.Factors.Select(f => f.Code).ShouldBe(["factor_debt_load", "factor_business_age", "factor_expense_ratio"]);
        outcome.Factors[0].Impact.ShouldBe(-35);
        outcome.Factors[0].Title.ShouldBe("Кредитная нагрузка");
        outcome.Factors[2].Value.ShouldBe("0%");
        outcome.Factors[2].ClientExplanation.ShouldBe("Расходы заметно ниже выручки");
        outcome.Warnings.ShouldBeEmpty();
        outcome.MissingData.ShouldBeEmpty();
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Request_follows_the_contract()
    {
        string? body = null;
        string? contentType = null;
        Uri? uri = null;
        var handler = new StubHandler(async (request, _) =>
        {
            uri = request.RequestUri;
            contentType = request.Content!.Headers.ContentType!.MediaType;
            body = await request.Content.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, ExampleResponse);
        });
        var request = Request(netProfit: null);

        await CreateService(handler).ScoreAsync(request, CancellationToken.None);

        uri!.ToString().ShouldBe("http://scoring.test/api/v1/score");
        contentType.ShouldBe("application/json");
        using var document = JsonDocument.Parse(body!);
        var json = document.RootElement;
        json.GetProperty("request_id").GetGuid().ShouldBe(request.RequestId);

        var application = json.GetProperty("application");
        application.GetProperty("number").GetString().ShouldBe("AC-000001");
        application.GetProperty("created_at").GetString().ShouldBe("2026-10-20T10:17:20Z", "метки времени в UTC с суффиксом Z");
        application.GetProperty("amount_rub").GetRawText().ShouldBe("1500000");
        application.GetProperty("estimated_monthly_payment_rub").GetRawText().ShouldBe("138593");
        application.GetProperty("purpose").GetString().ShouldBe("WorkingCapital");
        application.GetProperty("collateral_offered").GetBoolean().ShouldBeFalse();
        var product = application.GetProperty("product");
        product.GetProperty("type").GetString().ShouldBe("WorkingCapital");
        product.GetProperty("base_rate_percent").GetDecimal().ShouldBe(19.5m);
        product.GetProperty("auto_approval_limit_rub").GetRawText().ShouldBe("5000000");

        var company = json.GetProperty("company");
        company.GetProperty("legal_form").GetString().ShouldBe("Ooo");
        company.GetProperty("tax_regime").GetString().ShouldBe("Osno");
        company.GetProperty("registered_at").GetString().ShouldBe("2019-03-12");
        company.GetProperty("business_age_months").GetInt32().ShouldBe(91);
        var financials = company.GetProperty("financials");
        financials.GetProperty("revenue_rub").GetRawText().ShouldBe("48000000");
        financials.GetProperty("net_profit_rub").ValueKind.ShouldBe(JsonValueKind.Null, "пропуски передаются явным null");
        company.GetProperty("history").GetProperty("previous_rejections").GetInt32().ShouldBe(0);

        var checks = json.GetProperty("checks");
        checks.GetProperty("egrul").GetProperty("status").GetString().ShouldBe("Active");
        checks.GetProperty("fns").GetProperty("tax_debt_rub").GetRawText().ShouldBe("0");
        checks.GetProperty("credit_bureau").GetProperty("max_overdue_days_24m").GetInt32().ShouldBe(0);
        checks.GetProperty("credit_bureau").GetProperty("has_write_offs").GetBoolean().ShouldBeFalse();
        checks.GetProperty("fssp").GetProperty("open_proceedings").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Server_errors_are_retried_and_then_rules_fallback_is_used()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.InternalServerError, """{"error":"model_unavailable"}""")));

        var outcome = await CreateService(handler, retries: 2).ScoreAsync(Request(), CancellationToken.None);

        handler.Calls.ShouldBe(3);
        outcome.Source.ShouldBe(ScoringSource.RulesFallback);
        outcome.ModelVersion.ShouldBe(RuleBasedScoringService.ModelVersion);
        outcome.Warnings.ShouldContain(HttpScoringService.UnavailableWarning);
        outcome.Score.ShouldBeInRange(0, 100);
        outcome.Factors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Transient_error_is_retried_until_success()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) => Task.FromResult(++calls == 1
            ? Json(HttpStatusCode.BadGateway, string.Empty)
            : Json(HttpStatusCode.OK, ExampleResponse)));

        var outcome = await CreateService(handler, retries: 2).ScoreAsync(Request(), CancellationToken.None);

        outcome.Source.ShouldBe(ScoringSource.External);
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Validation_error_is_not_retried()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(
            HttpStatusCode.BadRequest,
            """{"error":"validation","details":{"company.financials.revenue_rub":"must be >= 0"}}""")));

        var outcome = await CreateService(handler, retries: 2).ScoreAsync(Request(), CancellationToken.None);

        handler.Calls.ShouldBe(1, "повтор того же payload не поможет");
        outcome.Source.ShouldBe(ScoringSource.RulesFallback);
    }

    [Fact]
    public async Task Timeout_falls_back_to_rules()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return Json(HttpStatusCode.OK, ExampleResponse);
        });

        var outcome = await CreateService(handler, retries: 1, timeout: TimeSpan.FromMilliseconds(200)).ScoreAsync(Request(), CancellationToken.None);

        handler.Calls.ShouldBe(2);
        outcome.Source.ShouldBe(ScoringSource.RulesFallback);
    }

    [Fact]
    public async Task Connection_failure_falls_back_to_rules()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("Connection refused (scoring:8000)"));

        var outcome = await CreateService(handler, retries: 1).ScoreAsync(Request(), CancellationToken.None);

        handler.Calls.ShouldBe(2);
        outcome.Source.ShouldBe(ScoringSource.RulesFallback);
    }

    [Fact]
    public async Task Malformed_response_falls_back_to_rules()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "<html>not json</html>")));

        var outcome = await CreateService(handler, retries: 0).ScoreAsync(Request(), CancellationToken.None);

        outcome.Source.ShouldBe(ScoringSource.RulesFallback);
    }

    [Fact]
    public async Task Unexpected_values_in_response_are_tolerated()
    {
        const string response = """
            {
              "request_id": 42,
              "model_version": "",
              "score": "77.6",
              "risk_grade": "unknown",
              "probability_of_default": 1.7,
              "factors": [ { "code": "x", "impact": 3.4, "value": 12 }, null ],
              "warnings": null,
              "missing_data": [ "checks.fssp", 7, null ],
              "extra_field": { "ignored": true }
            }
            """;
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, response)));

        var outcome = await CreateService(handler).ScoreAsync(Request(), CancellationToken.None);

        outcome.Score.ShouldBe(78);
        outcome.Grade.ShouldBe(RiskGrade.B, "неизвестный грейд выводится из балла");
        outcome.ProbabilityOfDefault.ShouldBe(1m);
        outcome.ModelVersion.ShouldBe("external");
        var factor = outcome.Factors.ShouldHaveSingleItem();
        factor.Code.ShouldBe("x");
        factor.Title.ShouldBe("x");
        factor.Impact.ShouldBe(3);
        factor.Value.ShouldBe("12");
        outcome.Warnings.ShouldBeEmpty();
        outcome.MissingData.ShouldBe(["checks.fssp", "7"]);
    }

    [Fact]
    public async Task Shutdown_cancellation_is_not_swallowed()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return Json(HttpStatusCode.OK, ExampleResponse);
        });
        var task = CreateService(handler, retries: 2).ScoreAsync(Request(), cancellation.Token);

        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(task);
        handler.Calls.ShouldBe(1);
    }

    private static HttpScoringService CreateService(HttpMessageHandler handler, int retries = 0, TimeSpan? timeout = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://scoring.test/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
        };
        var options = Options.Create(new ScoringOptions
        {
            Mode = ScoringMode.External,
            BaseUrl = "http://scoring.test",
            Retries = retries,
            RetryDelayMs = 0,
            TimeoutSeconds = 5,
        });
        return new HttpScoringService(client, options, NullLogger<HttpScoringService>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static ScoringRequest Request(decimal? netProfit = 7_200_000) => new(
        Guid.CreateVersion7(),
        new ScoringApplication(
            Guid.NewGuid(),
            "AC-000001",
            new DateTimeOffset(2026, 10, 20, 13, 17, 20, TimeSpan.FromHours(3)),
            1_500_000,
            12,
            LoanPurpose.WorkingCapital,
            138_593,
            new ScoringProduct("working-capital", ProductType.WorkingCapital, 19.5m, false, 5_000_000),
            false),
        new ScoringCompany(
            "7701234567",
            LegalForm.Ooo,
            "47.11",
            "77",
            new DateOnly(2019, 3, 12),
            91,
            TaxRegime.Osno,
            24,
            new ScoringFinancials(12, 48_000_000, 40_800_000, netProfit, 150_000),
            new ScoringHistory(0, 0, 0)),
        new ScoringChecks(
            new EgrulResult(EgrulStatus.Active, false, false, new DateOnly(2019, 3, 12), "47.11"),
            new FnsResult(0, false, 47_300_000),
            new CreditBureauResult(60, 1, 3, 1_200_000, 150_000, 0, 0, false),
            new FsspResult(0, 0)));

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return respond(request, cancellationToken);
        }
    }
}
