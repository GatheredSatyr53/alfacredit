using AlfaCredit.Api.Contracts.Feed;
using AlfaCredit.Api.Mapping;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Domain.Users;
using AlfaCredit.Core.Features.Decisions;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

// Проверки границы API без Docker: те же сохранённые сущности читают клиент, менеджер и администратор.
public sealed class ClientDecisionPresentationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(RiskGrade.A, 98, DecisionType.Approved)]
    [InlineData(RiskGrade.B, 67, DecisionType.Approved)]
    [InlineData(RiskGrade.C, 57, DecisionType.ApprovedWithConditions)]
    [InlineData(RiskGrade.D, 40, DecisionType.ManualReview)]
    [InlineData(RiskGrade.E, 20, DecisionType.Rejected)]
    public void Client_sees_the_actual_decision_and_bank_keeps_scoring(RiskGrade grade, int score, DecisionType expected)
    {
        var application = Application();
        var scoring = ScoringResult.Create(application.Id, Guid.NewGuid(),
            new ScoringOutcome(score, grade, 0.15m,
                [new ScoringFactor("debt_load", "Долговая нагрузка", -5, "0.5", "Платежи занимают половину прибыли.")],
                [], [], "logreg-synthetic-0.1", ScoringSource.External), 1, Now);
        application.RecordScoring(scoring);
        var checks = new ChecksSnapshot(
            new EgrulResult(EgrulStatus.Active, false, false, new DateOnly(2019, 3, 12), "47.11"),
            new FnsResult(0, false, null), new CreditBureauResult(60, 1, 3, 0, 0, 0, 0, false), new FsspResult(0, 0));
        var outcome = new DecisionPolicy(new DecisionPolicyOptions()).Decide(application, checks, scoring);
        var decision = Decision.Create(application.Id, outcome.Type, DecisionActor.Auto, null, outcome.RecommendedType,
            outcome.Amount, outcome.TermMonths, outcome.InterestRate, outcome.MonthlyPayment, outcome.Conditions, outcome.Reasons, Now);
        application.ApplyDecision(decision, TimeSpan.FromDays(7), Now);

        var client = application.ToDto(new Actor(application.CreatedByUserId, UserRole.Client), Now);
        client.Decision.ShouldNotBeNull().Type.ShouldBe(expected);
        client.Decision.Reasons.Single(r => r.Code == "risk_grade").Title.ShouldBe("Результат оценки");
        client.Decision.Reasons.Single(r => r.Code == "factor_debt_load").Explanation.ShouldBe("Платежи занимают половину прибыли.");
        client.Scoring.ShouldBeNull();
        client.Checks.ShouldBeNull();
        client.History.ShouldAllBe(h => h.Comment == null || !h.Comment.Contains("Грейд риска", StringComparison.OrdinalIgnoreCase));

        foreach (var role in new[] { UserRole.Manager, UserRole.Admin })
        {
            var bank = application.ToDto(new Actor(Guid.NewGuid(), role), Now);
            bank.Scoring.ShouldNotBeNull().Grade.ShouldBe(grade);
            bank.Scoring.Score.ShouldBe(score);
            bank.Scoring.ProbabilityOfDefault.ShouldBe(0.15m);
            bank.Scoring.ModelVersion.ShouldBe("logreg-synthetic-0.1");
            bank.Decision.ShouldNotBeNull().Reasons.Single(r => r.Code == "risk_grade").Title.ShouldBe($"Грейд риска {grade}, {score} баллов");
            client.Decision.Type.ShouldBe(bank.Decision.Type);
            client.Decision.ApprovedAmount.ShouldBe(bank.Decision.ApprovedAmount);
            client.Decision.InterestRate.ShouldBe(bank.Decision.InterestRate);
            client.Decision.ApprovedTermMonths.ShouldBe(bank.Decision.ApprovedTermMonths);
            client.Decision.MonthlyPayment.ShouldBe(bank.Decision.MonthlyPayment);
            client.Offer.ShouldBe(bank.Offer);
        }
        decision.Reasons.Single(r => r.Code == "risk_grade").Title.ShouldBe($"Грейд риска {grade}, {score} баллов");
    }

    [Fact]
    public void Saved_handoff_history_and_legacy_conditions_are_presented_without_rewriting_records()
    {
        var application = Application();
        var handoff = Decision.Create(application.Id, DecisionType.ManualReview, DecisionActor.Auto, null,
            DecisionType.ApprovedWithConditions, null, null, null, null, null,
            [new DecisionReason("amount_above_auto_limit", "Крупная сумма", "Нужно подтверждение менеджера."),
                new DecisionReason("risk_grade", "Грейд риска B, 70 баллов", "Уровень риска ниже среднего.")], Now);
        application.ApplyDecision(handoff, TimeSpan.FromDays(7), Now);
        const string conditions = "ставка 21 % годовых с учётом надбавки за риск (грейд B); до выдачи предоставить выписку";
        var approval = Decision.Create(application.Id, DecisionType.ApprovedWithConditions, DecisionActor.Manager,
            Guid.NewGuid(), null, 2_400_000, 24, 21, 123_333, conditions,
            [DemoManagerPolicy.SimulatedReason], Now.AddMinutes(1), isSimulated: true);
        application.ApplyDecision(approval, TimeSpan.FromDays(7), Now.AddMinutes(1));

        var client = application.ToDto(new Actor(application.CreatedByUserId, UserRole.Client), Now);
        client.Decision.ShouldNotBeNull().Conditions.ShouldBe("ставка 21 % годовых с учётом надбавки за риск; до выдачи предоставить выписку");
        client.Decision.IsSimulated.ShouldBeTrue();
        client.History.Single(h => h.ToStatus == ApplicationStatus.ManualReview).Comment.ShouldBe("Передана менеджеру. Крупная сумма. Результат оценки.");
        var bank = application.ToDto(new Actor(Guid.NewGuid(), UserRole.Manager), Now);
        bank.Decision.ShouldNotBeNull().Conditions.ShouldBe(conditions);
        bank.History.Single(h => h.ToStatus == ApplicationStatus.ManualReview).Comment!.ShouldContain("Грейд риска B, 70 баллов");
        approval.Conditions.ShouldBe(conditions);
        application.History.Single(h => h.ToStatus == ApplicationStatus.ManualReview).Comment!.ShouldContain("Грейд риска B, 70 баллов");
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    public void Old_feed_items_use_the_same_role_aware_presentation(UserRole role)
    {
        const string title = "Грейд риска B, 70 баллов";
        const string body = "Ставка 21 % (грейд B); предоставить выписку. Ответ менеджера смоделирован (демо-режим).";
        var item = FeedItem.Create(Guid.NewGuid(), Guid.NewGuid(), FeedItemType.OfferReady, title, body, "/applications/example", Now);
        var actor = new Actor(item.UserId, role);
        var dto = FeedItemDto.From(item, actor);
        dto.Title.ShouldBe(role == UserRole.Client ? "Результат оценки" : title);
        dto.Body.ShouldBe(role == UserRole.Client ? "Ставка 21 %; предоставить выписку. Ответ менеджера смоделирован (демо-режим)." : body);
        item.MarkRead(Now);
        var read = FeedItemDto.From(item, actor);
        read.IsRead.ShouldBeTrue();
        read.Body.ShouldBe(dto.Body);
        item.Body.ShouldBe(body);
        item.Title.ShouldBe(title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Налоговый долг 500 000 ₽. Сумма снижена до 2 400 000 ₽, срок 24 мес., ставка 22,5 %.")]
    [InlineData("Предоставьте документ класса A и выписку за 6 месяцев.")]
    public void Ordinary_client_text_is_preserved(string? text)
    {
        ClientDecisionText.ForActor(text, new Actor(Guid.NewGuid(), UserRole.Client)).ShouldBe(text);
    }

    private static CreditApplication Application()
    {
        var company = Company.Create("7701234567", "1157746123456", "ООО «Ромашка»", LegalForm.Ooo, "47.11",
            new DateOnly(2019, 3, 12), "Москва", "Иванова Мария", TaxRegime.Osno, 9_600_000, 4_800_000,
            4_800_000, 0, 24, Now);
        var product = CreditProduct.Create("working-capital", "Оборотный кредит", "Пополнение оборотных средств",
            ProductType.WorkingCapital, 300_000, 10_000_000, 3, 24, 19.5m, [LoanPurpose.WorkingCapital], 12, false, 5_000_000, 1);
        var application = CreditApplication.Create(company, product, Guid.NewGuid(), 3_000_000, 24, LoanPurpose.WorkingCapital, null, Now);
        application.BeginValidation(Now);
        application.BeginEnrichment(Now);
        application.BeginScoring(Now);
        application.BeginDeciding(Now);
        return application;
    }
}
