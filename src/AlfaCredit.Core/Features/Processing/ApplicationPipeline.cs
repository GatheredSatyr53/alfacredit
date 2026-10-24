using System.Diagnostics;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Feed;
using AlfaCredit.Core.Features.Scoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Core.Features.Processing;

/// <summary>
/// Автоматический конвейер банка: проверка данных, обогащение внешними сведениями, скоринг, решение, выдача по подписанному договору.
/// Каждый этап выполняется отдельным заданием из базы, между этапами настраиваемая пауза для наглядности демо.
/// Этапы идемпотентны по статусу заявки: после перезапуска сервиса задание выполняется заново без дублей.
/// </summary>
public sealed class ApplicationPipeline(
    ICreditApplicationRepository applications,
    IProcessingJobRepository jobs,
    IEgrulClient egrul,
    IFnsClient fns,
    ICreditBureauClient creditBureau,
    IFsspClient fssp,
    IScoringService scoring,
    IDisbursementGateway disbursements,
    DecisionPolicy policy,
    FeedService feed,
    IApplicationNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<ProcessingOptions> processingOptions,
    IOptions<DecisionPolicyOptions> policyOptions,
    IOptions<DemoManagerOptions> demoManager,
    IDemoManagerChooser demoChooser,
    ILogger<ApplicationPipeline> logger)
{
    private const int SourceAttempts = 3;
    private static readonly TimeSpan SourceRetryDelay = TimeSpan.FromMilliseconds(300);

    public async Task ExecuteAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var application = await applications.FindByIdAsync(job.ApplicationId, cancellationToken)
            ?? throw new InvalidOperationException($"Application {job.ApplicationId} not found for job {job.Id}.");

        if (application.IsTerminal)
        {
            logger.LogInformation("Application {Number} is already {Status}, job {Stage} skipped", application.Number, application.Status, job.Stage);
            job.MarkDone(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var next = job.Stage switch
        {
            ProcessingStage.Validate => Validate(application),
            ProcessingStage.Enrich => await EnrichAsync(application, cancellationToken),
            ProcessingStage.Score => await ScoreAsync(application, cancellationToken),
            ProcessingStage.Decide => await DecideAsync(application, cancellationToken),
            ProcessingStage.Disburse => await DisburseAsync(application, cancellationToken),
            ProcessingStage.ManagerDecision => await ManagerDecisionAsync(application, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(job), job.Stage, "Unknown processing stage."),
        };

        var now = clock.GetUtcNow();
        job.MarkDone(now);
        if (next is { } nextStage)
        {
            jobs.Add(ProcessingJob.Create(application.Id, nextStage, now, processingOptions.Value.StageDelay, processingOptions.Value.MaxAttempts));
        }
        else if (application.Status == ApplicationStatus.ManualReview && demoManager.Value.Enabled && job.Stage != ProcessingStage.ManagerDecision)
        {
            // Демо-режим: ответ менеджера моделируется отдельным заданием с задержкой, чтобы ручное рассмотрение было видно в кабинете
            jobs.Add(ProcessingJob.Create(application.Id, ProcessingStage.ManagerDecision, now, demoManager.Value.Delay, processingOptions.Value.MaxAttempts));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifySafeAsync(application, cancellationToken);
        logger.LogInformation("Application {Number}: stage {Stage} finished, status {Status}", application.Number, job.Stage, application.Status);
    }

    private ProcessingStage? Validate(CreditApplication application)
    {
        if (application.Status != ApplicationStatus.Submitted)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        application.BeginValidation(now);

        var issues = ValidationIssues(application).ToList();
        if (issues.Count == 0)
        {
            return ProcessingStage.Enrich;
        }

        var text = string.Join(" ", issues);
        application.RequestClarification($"Для продолжения рассмотрения нужны уточнения: {text}", ActorType.System, null, clock.GetUtcNow());
        feed.NotifyCompany(application, FeedItemType.ClarificationRequested, $"По заявке {application.Number} нужны уточнения", text);
        return null;
    }

    private static IEnumerable<string> ValidationIssues(CreditApplication application)
    {
        var company = application.Company;
        if (company.AnnualRevenue <= 0)
        {
            yield return "Укажите выручку компании за последние 12 месяцев.";
        }

        if (string.IsNullOrWhiteSpace(company.Ogrn))
        {
            yield return "Укажите ОГРН или ОГРНИП.";
        }

        var needsStatements = ValidationRules.NeedsFinancialStatements(application.Amount, application.Product.Type);
        if (needsStatements && (company.AnnualExpenses is null || company.NetProfit is null))
        {
            yield return "Для этой суммы и продукта нужны расходы и чистая прибыль за последние 12 месяцев.";
        }
    }

    private async Task<ProcessingStage?> EnrichAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        if (application.Status != ApplicationStatus.Validating)
        {
            return null;
        }

        application.BeginEnrichment(clock.GetUtcNow());
        var company = application.Company;

        var results = await Task.WhenAll(
            FetchAsync(application.Id, CheckSource.Egrul, () => egrul.GetAsync(company, cancellationToken), SummarizeEgrul, cancellationToken),
            FetchAsync(application.Id, CheckSource.Fns, () => fns.GetAsync(company, cancellationToken), SummarizeFns, cancellationToken),
            FetchAsync(application.Id, CheckSource.CreditBureau, () => creditBureau.GetAsync(company, cancellationToken), SummarizeBureau, cancellationToken),
            FetchAsync(application.Id, CheckSource.Fssp, () => fssp.GetAsync(company, cancellationToken), SummarizeFssp, cancellationToken));

        foreach (var check in results)
        {
            application.RecordCheck(check);
        }

        var failedCritical = results.Where(r => r.Status == CheckStatus.Failed && r.Source.IsCritical()).Select(r => r.Source.Title()).ToList();
        if (failedCritical.Count > 0)
        {
            var text = string.Join(", ", failedCritical);
            application.RouteToManualReview($"Внешняя проверка недоступна: {text}. Заявка передана менеджеру.", clock.GetUtcNow());
            feed.NotifyCompany(application, FeedItemType.ManualReview, $"Заявка {application.Number} передана менеджеру", "Часть внешних проверок временно недоступна, менеджер продолжит рассмотрение вручную.");
            await feed.NotifyManagersAsync(application, FeedItemType.ManualReview, $"Заявка {application.Number} требует ручного рассмотрения", $"Недоступны источники: {text}.", cancellationToken);
            return null;
        }

        return ProcessingStage.Score;
    }

    private async Task<ExternalCheck> FetchAsync<T>(
        Guid applicationId,
        CheckSource source,
        Func<Task<T>> fetch,
        Func<T, (string Summary, IReadOnlyList<string> Flags)> summarize,
        CancellationToken cancellationToken)
        where T : class
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= SourceAttempts; attempt++)
        {
            try
            {
                var payload = await fetch();
                var (summary, flags) = summarize(payload);
                return ExternalCheck.Succeeded(applicationId, source, payload, summary, flags, attempt, clock.GetUtcNow());
            }
            catch (ExternalSourceUnavailableException ex)
            {
                last = ex;
                logger.LogWarning("Source {Source} unavailable (attempt {Attempt}/{Max}): {Message}", source, attempt, SourceAttempts, ex.Message);
                if (attempt < SourceAttempts)
                {
                    await Task.Delay(SourceRetryDelay * attempt, cancellationToken);
                }
            }
        }

        return ExternalCheck.Failed(applicationId, source, last?.Message ?? "unknown error", SourceAttempts, clock.GetUtcNow());
    }

    private static (string, IReadOnlyList<string>) SummarizeEgrul(EgrulResult r)
    {
        var flags = new List<string>();
        if (r.Status != EgrulStatus.Active)
        {
            flags.Add("company_inactive");
        }

        if (r.HeadDisqualified)
        {
            flags.Add("head_disqualified");
        }

        if (r.MassAddress)
        {
            flags.Add("mass_address");
        }

        var status = r.Status switch { EgrulStatus.Active => "действующая", EgrulStatus.Liquidating => "в стадии ликвидации", _ => "прекратила деятельность" };
        return ($"Компания {status}, зарегистрирована {r.RegisteredAt:dd.MM.yyyy}, ОКВЭД {r.Okved}." + (r.MassAddress ? " Адрес массовой регистрации." : string.Empty) + (r.HeadDisqualified ? " Руководитель дисквалифицирован." : string.Empty), flags);
    }

    private static (string, IReadOnlyList<string>) SummarizeFns(FnsResult r)
    {
        var flags = new List<string>();
        if (r.TaxDebtRub > 0)
        {
            flags.Add("tax_debt");
        }

        if (r.HasBlockedAccounts)
        {
            flags.Add("accounts_blocked");
        }

        var debt = r.TaxDebtRub > 0 ? $"Налоговая задолженность {Money.FormatRub(r.TaxDebtRub)}." : "Налоговой задолженности нет.";
        var blocked = r.HasBlockedAccounts ? " Есть блокировка счетов." : string.Empty;
        var revenue = r.ReportedRevenueRub is { } rev ? $" Выручка по отчётности {Money.FormatRub(rev)}." : string.Empty;
        return (debt + blocked + revenue, flags);
    }

    private static (string, IReadOnlyList<string>) SummarizeBureau(CreditBureauResult r)
    {
        var flags = new List<string>();
        if (r.MaxOverdueDays24M > 90 || r.HasWriteOffs)
        {
            flags.Add("overdue_90");
        }
        else if (r.MaxOverdueDays24M > 0)
        {
            flags.Add("overdue");
        }

        if (r.CreditHistoryMonths == 0 && r.ActiveLoans == 0 && r.ClosedLoans == 0)
        {
            return ("Кредитной истории нет.", flags);
        }

        var overdue = r.MaxOverdueDays24M == 0 ? "без просрочек" : $"максимальная просрочка {r.MaxOverdueDays24M} дн.";
        return ($"История {r.CreditHistoryMonths} мес., активных кредитов {r.ActiveLoans}, задолженность {Money.FormatRub(r.TotalDebtRub)}, платежи {Money.FormatRub(r.MonthlyPaymentsRub)} в месяц, {overdue}.", flags);
    }

    private static (string, IReadOnlyList<string>) SummarizeFssp(FsspResult r)
    {
        var flags = new List<string>();
        if (r.OpenProceedings > 0)
        {
            flags.Add("enforcement");
        }

        return (r.OpenProceedings == 0 ? "Открытых исполнительных производств нет." : $"Открытых производств: {r.OpenProceedings} на {Money.FormatRub(r.OpenAmountRub)}.", flags);
    }

    private async Task<ProcessingStage?> ScoreAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        if (application.Status != ApplicationStatus.Enriching)
        {
            return null;
        }

        application.BeginScoring(clock.GetUtcNow());

        var history = await applications.GetCompanyHistoryAsync(application.CompanyId, application.Id, cancellationToken);
        var request = BuildScoringRequest(application, history, clock.GetUtcNow());

        var stopwatch = Stopwatch.StartNew();
        var outcome = await scoring.ScoreAsync(request, cancellationToken);
        stopwatch.Stop();

        // Чего не хватало, backend знает по отправленному запросу; сервис скоринга может дополнить список
        var missing = request.MissingInputs().Union(outcome.MissingData, StringComparer.Ordinal).ToArray();
        var result = ScoringResult.Create(application.Id, request.RequestId, outcome with { MissingData = missing }, (int)stopwatch.ElapsedMilliseconds, clock.GetUtcNow());
        application.RecordScoring(result);
        logger.LogInformation(
            "Application {Number}: scoring {Source} ({Model}) gave {Score} points, grade {Grade}, {Elapsed} ms, request {RequestId}",
            application.Number, result.Source, result.ModelVersion, result.Score, result.Grade, result.DurationMs, request.RequestId);

        return ProcessingStage.Decide;
    }

    /// <summary>Собирает запрос по docs/scoring-contract.md: деньги в целых рублях, пропуски явным null, платёж по базовой ставке продукта.</summary>
    public static ScoringRequest BuildScoringRequest(CreditApplication application, CompanyApplicationHistory history, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(history);

        var company = application.Company;
        var product = application.Product;
        var today = BankCalendar.Today(now);

        return new ScoringRequest(
            Guid.CreateVersion7(),
            new ScoringApplication(
                application.Id,
                application.Number,
                application.CreatedAt,
                Money.WholeRubles(application.Amount),
                application.TermMonths,
                application.Purpose,
                Money.WholeRubles(LoanMath.MonthlyPayment(application.Amount, product.BaseRate, application.TermMonths)),
                new ScoringProduct(product.Code, product.Type, product.BaseRate, product.RequiresCollateral, Money.WholeRubles(product.AutoApprovalLimit)),
                CollateralOffered: false),
            new ScoringCompany(
                company.Inn,
                company.LegalForm,
                company.Okved,
                company.RegionCode,
                company.RegisteredAt,
                company.GetBusinessAgeMonths(today),
                company.TaxRegime,
                company.EmployeesCount,
                new ScoringFinancials(
                    12,
                    Money.WholeRubles(company.AnnualRevenue),
                    Money.WholeRubles(company.AnnualExpenses),
                    Money.WholeRubles(company.NetProfit),
                    Money.WholeRubles(company.MonthlyDebtPayments)),
                new ScoringHistory(history.PreviousApplications, history.PreviousRejections, history.PreviousLoansDisbursed)),
            ScoringChecks.From(application.ChecksSnapshot));
    }

    private async Task<ProcessingStage?> DecideAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        if (application.Status is not (ApplicationStatus.Scoring or ApplicationStatus.Deciding))
        {
            return null;
        }

        if (application.Status == ApplicationStatus.Scoring)
        {
            application.BeginDeciding(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await NotifySafeAsync(application, cancellationToken);

            // Пауза, чтобы статус «Принятие решения» был виден в кабинете
            if (processingOptions.Value.StageDelay > TimeSpan.Zero)
            {
                await Task.Delay(processingOptions.Value.StageDelay, cancellationToken);
            }
        }
        else
        {
            // Статус уже сохранён, а решения нет: сервис был остановлен посреди этапа, продолжаем с места остановки
            logger.LogInformation("Application {Number}: decide stage resumed after interruption", application.Number);
        }

        var scoringResult = application.LatestScoring
            ?? throw new InvalidOperationException($"Application {application.Number} has no scoring result.");
        var outcome = policy.Decide(application, application.ChecksSnapshot, scoringResult);
        var now = clock.GetUtcNow();

        var decision = Decision.Create(
            application.Id,
            outcome.Type,
            DecisionActor.Auto,
            null,
            outcome.RecommendedType,
            outcome.Amount,
            outcome.TermMonths,
            outcome.InterestRate,
            outcome.MonthlyPayment,
            outcome.Conditions,
            outcome.Reasons,
            now);

        var offer = application.ApplyDecision(decision, policyOptions.Value.OfferValidity, now);
        await PublishDecisionAsync(application, decision, offer, cancellationToken);
        return null;
    }

    /// <summary>Выдача: перевод через банковское ядро. Неудачная попытка сохраняется и уходит на повтор, успешная завершает заявку.</summary>
    private async Task<ProcessingStage?> DisburseAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        if (application.Status != ApplicationStatus.Signed)
        {
            return null;
        }

        var contract = application.CurrentContract
            ?? throw new InvalidOperationException($"Application {application.Number} is signed but has no contract.");
        var requestedAt = clock.GetUtcNow();

        DisbursementReceipt receipt;
        try
        {
            receipt = await disbursements.TransferAsync(contract, cancellationToken);
        }
        catch (DisbursementFailedException ex)
        {
            application.RecordDisbursement(Disbursement.Failed(application.Id, contract, ex.Message, requestedAt), clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        var disbursement = Disbursement.Completed(application.Id, contract, receipt.PaymentOrderNumber, requestedAt, receipt.ProcessedAt);
        application.RecordDisbursement(disbursement, clock.GetUtcNow());

        feed.NotifyCompany(application, FeedItemType.Disbursed, $"Средства по договору {contract.Number} выданы",
            $"{Money.FormatRub(disbursement.Amount)} зачислены на расчётный счёт {disbursement.Account}, платёжное поручение {disbursement.PaymentOrderNumber}. Первый платёж {Money.FormatRub(contract.MonthlyPayment)} через месяц.");
        await feed.NotifyManagersAsync(application, FeedItemType.Disbursed, $"Выдача по договору {contract.Number} завершена",
            $"{application.Company.Name}: {Money.FormatRub(disbursement.Amount)}, платёжное поручение {disbursement.PaymentOrderNumber}.", cancellationToken);
        return null;
    }

    /// <summary>
    /// Демо-режим: смоделированный ответ менеджера. Исход выбирается случайно по настроенной вероятности, решение сохраняется
    /// с пометкой IsSimulated и попадает в историю. Если заявку уже отменили или решили, задание ничего не делает.
    /// </summary>
    private async Task<ProcessingStage?> ManagerDecisionAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        if (!demoManager.Value.Enabled || application.Status != ApplicationStatus.ManualReview)
        {
            logger.LogInformation("Application {Number}: simulated manager decision skipped (demo mode {Enabled}, status {Status})", application.Number, demoManager.Value.Enabled, application.Status);
            return null;
        }

        var now = clock.GetUtcNow();
        var decision = demoChooser.Approves() ? DemoManagerPolicy.Approve(application, now) : DemoManagerPolicy.Reject(application, now);
        var offer = application.ApplyDecision(decision, policyOptions.Value.OfferValidity, now);
        logger.LogInformation("Application {Number}: simulated manager decision {Type} (demo mode)", application.Number, decision.Type);

        var number = application.Number;
        switch (decision.Type)
        {
            case DecisionType.Approved or DecisionType.ApprovedWithConditions when offer is not null:
                var conditions = string.IsNullOrWhiteSpace(decision.Conditions) ? string.Empty : $" Условия: {decision.Conditions}.";
                feed.NotifyCompany(application, FeedItemType.OfferReady, $"Заявка {number} одобрена менеджером",
                    $"Банк готов выдать {Money.FormatRub(offer.Amount)} на {offer.TermMonths} мес. под {offer.InterestRate:0.##} % годовых, платёж {Money.FormatRub(offer.MonthlyPayment)} в месяц.{conditions} Предложение действует до {offer.ExpiresAt:dd.MM.yyyy}. Ответ менеджера смоделирован (демо-режим).");
                break;

            case DecisionType.Rejected:
                feed.NotifyCompany(application, FeedItemType.Rejected, $"По заявке {number} менеджер принял отрицательное решение",
                    $"{decision.Reasons[0].ClientExplanation} Ответ менеджера смоделирован (демо-режим).");
                break;
        }

        await feed.NotifyManagersAsync(application, FeedItemType.StatusChanged, $"Демо-менеджер: {decision.Type} по заявке {number}",
            $"{application.Company.Name}, {Money.FormatRub(application.Amount)} на {application.TermMonths} мес. Ответ смоделирован демо-режимом (DemoManager).", cancellationToken);
        return null;
    }

    private async Task PublishDecisionAsync(CreditApplication application, Decision decision, Offer? offer, CancellationToken cancellationToken)
    {
        var number = application.Number;
        switch (decision.Type)
        {
            case DecisionType.Approved when offer is not null:
                feed.NotifyCompany(application, FeedItemType.OfferReady, $"Заявка {number} одобрена",
                    $"Банк готов выдать {Money.FormatRub(offer.Amount)} на {offer.TermMonths} мес. под {offer.InterestRate:0.##} % годовых, платёж {Money.FormatRub(offer.MonthlyPayment)} в месяц. Предложение действует до {offer.ExpiresAt:dd.MM.yyyy}.");
                break;

            case DecisionType.ApprovedWithConditions when offer is not null:
                feed.NotifyCompany(application, FeedItemType.OfferReady, $"Заявка {number} одобрена с изменением условий",
                    $"Новые условия: {Money.FormatRub(offer.Amount)} на {offer.TermMonths} мес. под {offer.InterestRate:0.##} % годовых, платёж {Money.FormatRub(offer.MonthlyPayment)} в месяц. {decision.Conditions}. Предложение действует до {offer.ExpiresAt:dd.MM.yyyy}.");
                break;

            case DecisionType.Rejected:
                var reasons = string.Join(" ", decision.Reasons.Take(3).Select(r => r.ClientExplanation));
                feed.NotifyCompany(application, FeedItemType.Rejected, $"По заявке {number} принято отрицательное решение", reasons);
                break;

            case DecisionType.ManualReview:
                feed.NotifyCompany(application, FeedItemType.ManualReview, $"Заявка {number} передана менеджеру",
                    "Автоматическая проверка завершена, окончательное решение примет кредитный менеджер. Возможен запрос документов.");
                await feed.NotifyManagersAsync(application, FeedItemType.ManualReview, $"Новая заявка {number} в очереди",
                    $"{application.Company.Name}, {Money.FormatRub(application.Amount)} на {application.TermMonths} мес. Рекомендация автоматики: {decision.RecommendedType}.", cancellationToken);
                break;
        }
    }

    private async Task NotifySafeAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.ApplicationChangedAsync(
                new ApplicationChangedNotification(
                    application.Id,
                    application.Number,
                    application.CompanyId,
                    application.Status,
                    ApplicationStatusCatalog.Get(application.Status).Title,
                    application.UpdatedAt),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Realtime notification failed for application {Number}", application.Number);
        }
    }
}
