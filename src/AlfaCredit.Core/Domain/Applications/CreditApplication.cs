using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;

namespace AlfaCredit.Core.Domain.Applications;

/// <summary>Кредитная заявка: агрегат, через который проходит весь путь от подачи до выдачи.</summary>
public sealed class CreditApplication
{
    public const int NumberMaxLength = 32;
    public const int PurposeDescriptionMaxLength = 1000;

    private readonly List<ApplicationStatusHistory> _history = [];
    private readonly List<ExternalCheck> _checks = [];
    private readonly List<ScoringResult> _scoringResults = [];
    private readonly List<Decision> _decisions = [];
    private readonly List<Offer> _offers = [];
    private readonly List<LoanContract> _contracts = [];
    private readonly List<Disbursement> _disbursements = [];

    private CreditApplication()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Человекочитаемый номер, например AC-000001. Генерируется базой данных при сохранении.</summary>
    public string Number { get; private set; } = null!;

    public Guid CompanyId { get; private set; }

    public Company Company { get; private set; } = null!;

    public Guid ProductId { get; private set; }

    public CreditProduct Product { get; private set; } = null!;

    public Guid CreatedByUserId { get; private set; }

    public decimal Amount { get; private set; }

    public int TermMonths { get; private set; }

    public LoanPurpose Purpose { get; private set; }

    public string? PurposeDescription { get; private set; }

    public ApplicationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Оптимистическая блокировка (PostgreSQL xmin): два решения по одной заявке невозможны.</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<ApplicationStatusHistory> History => _history.AsReadOnly();

    public IReadOnlyList<ExternalCheck> Checks => _checks.AsReadOnly();

    public IReadOnlyList<ScoringResult> ScoringResults => _scoringResults.AsReadOnly();

    public IReadOnlyList<Decision> Decisions => _decisions.AsReadOnly();

    public IReadOnlyList<Offer> Offers => _offers.AsReadOnly();

    public IReadOnlyList<LoanContract> Contracts => _contracts.AsReadOnly();

    public IReadOnlyList<Disbursement> Disbursements => _disbursements.AsReadOnly();

    public ScoringResult? LatestScoring => _scoringResults.MaxBy(s => s.ComputedAt);

    public Decision? LatestDecision => _decisions.MaxBy(d => d.DecidedAt);

    public Decision? FinalDecision => _decisions.Where(d => d.IsFinal).MaxBy(d => d.DecidedAt);

    public Offer? CurrentOffer => _offers.MaxBy(o => o.CreatedAt);

    public LoanContract? CurrentContract => _contracts.MaxBy(c => c.CreatedAt);

    public Disbursement? CurrentDisbursement => _disbursements.OrderBy(d => d.RequestedAt).LastOrDefault();

    public ChecksSnapshot ChecksSnapshot => ExternalCheck.ToSnapshot(_checks);

    public bool IsTerminal => ApplicationStatusCatalog.Get(Status).IsTerminal;

    /// <summary>Создаёт заявку и сразу отправляет её в банк. Все ошибки по полям возвращаются вместе.</summary>
    public static CreditApplication Create(
        Company company,
        CreditProduct product,
        Guid createdByUserId,
        decimal amount,
        int termMonths,
        LoanPurpose purpose,
        string? purposeDescription,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(product);

        var errors = new ValidationErrors();

        if (!product.IsActive)
        {
            errors.Add("productCode", $"Продукт «{product.Name}» сейчас недоступен для оформления.");
        }

        if (!product.AllowsAmount(amount))
        {
            errors.Add("amount", $"Для продукта «{product.Name}» сумма должна быть от {Money.FormatRub(product.MinAmount)} до {Money.FormatRub(product.MaxAmount)}.");
        }

        if (!product.AllowsTerm(termMonths))
        {
            errors.Add("termMonths", $"Для продукта «{product.Name}» срок должен быть от {product.MinTermMonths} до {product.MaxTermMonths} мес.");
        }

        if (!product.AllowsPurpose(purpose))
        {
            var allowed = string.Join(", ", product.AllowedPurposes.Select(p => $"«{p.Title()}»"));
            errors.Add("purpose", $"Цель «{purpose.Title()}» недоступна для продукта «{product.Name}». Доступные цели: {allowed}.");
        }

        var businessAgeMonths = company.GetBusinessAgeMonths(DateOnly.FromDateTime(now.UtcDateTime));
        if (businessAgeMonths < product.MinBusinessAgeMonths)
        {
            errors.Add("companyId", $"Возраст бизнеса {businessAgeMonths} мес., для продукта «{product.Name}» требуется не менее {product.MinBusinessAgeMonths} мес.");
        }

        if (purposeDescription is { Length: > PurposeDescriptionMaxLength })
        {
            errors.Add("purposeDescription", $"Описание цели не длиннее {PurposeDescriptionMaxLength} символов.");
        }

        errors.ThrowIfAny();

        var application = new CreditApplication
        {
            Id = Guid.CreateVersion7(),
            CompanyId = company.Id,
            Company = company,
            ProductId = product.Id,
            Product = product,
            CreatedByUserId = createdByUserId,
            Amount = amount,
            TermMonths = termMonths,
            Purpose = purpose,
            PurposeDescription = string.IsNullOrWhiteSpace(purposeDescription) ? null : purposeDescription.Trim(),
            Status = ApplicationStatus.Submitted,
            CreatedAt = now,
            UpdatedAt = now,
        };

        application._history.Add(ApplicationStatusHistory.Create(
            application.Id,
            fromStatus: null,
            ApplicationStatus.Submitted,
            ActorType.Client,
            createdByUserId,
            "Заявка создана и отправлена в банк.",
            now));

        return application;
    }

    public void ChangeStatus(ApplicationStatus to, ActorType actor, Guid? actorUserId, string? comment, DateTimeOffset now)
    {
        ApplicationWorkflow.EnsureAllowed(Status, to, actor);

        var from = Status;
        Status = to;
        UpdatedAt = now;
        _history.Add(ApplicationStatusHistory.Create(Id, from, to, actor, actorUserId, comment, now));
    }

    public void Cancel(Guid userId, string? reason, DateTimeOffset now)
    {
        var defaultReason = Status == ApplicationStatus.ContractSigning ? "Клиент отказался от подписания договора." : "Заявка отменена клиентом.";
        ChangeStatus(ApplicationStatus.Cancelled, ActorType.Client, userId, string.IsNullOrWhiteSpace(reason) ? defaultReason : reason, now);
        CurrentContract?.Cancel(now);
    }

    /// <summary>Действия, доступные участнику в текущем статусе (ключи для кнопок интерфейса).</summary>
    public IReadOnlyList<string> AvailableActions(ActorType actor) =>
        ApplicationWorkflow.NextStatuses(Status, actor)
            .Select(ActionName)
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct()
            .ToArray();

    // ---- Конвейер банка ----

    public void BeginValidation(DateTimeOffset now) =>
        ChangeStatus(ApplicationStatus.Validating, ActorType.System, null, "Автоматическая проверка полноты и корректности данных.", now);

    public void RequestClarification(string comment, ActorType actor, Guid? actorUserId, DateTimeOffset now) =>
        ChangeStatus(ApplicationStatus.NeedsClarification, actor, actorUserId, comment, now);

    public void BeginEnrichment(DateTimeOffset now) =>
        ChangeStatus(ApplicationStatus.Enriching, ActorType.System, null, "Запрос сведений из ЕГРЮЛ, ФНС, бюро кредитных историй и ФССП.", now);

    public void RecordCheck(ExternalCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        _checks.Add(check);
    }

    public void BeginScoring(DateTimeOffset now) =>
        ChangeStatus(ApplicationStatus.Scoring, ActorType.System, null, "Расчёт скоринговой оценки.", now);

    public void RecordScoring(ScoringResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _scoringResults.Add(result);
    }

    public void BeginDeciding(DateTimeOffset now) =>
        ChangeStatus(ApplicationStatus.Deciding, ActorType.System, null, "Применение кредитной политики банка.", now);

    public void RouteToManualReview(string reason, DateTimeOffset now) =>
        ChangeStatus(ApplicationStatus.ManualReview, ActorType.System, null, reason, now);

    /// <summary>Фиксирует решение, переводит статус и для одобрения создаёт предложение клиенту.</summary>
    public Offer? ApplyDecision(Decision decision, TimeSpan offerValidity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var actor = decision.DecidedBy == DecisionActor.Manager ? ActorType.Manager : ActorType.System;
        var who = decision.DecidedBy == DecisionActor.Manager ? "Решение менеджера" : "Автоматическое решение";

        _decisions.Add(decision);

        switch (decision.Type)
        {
            case DecisionType.Approved:
            case DecisionType.ApprovedWithConditions:
                var status = decision.Type == DecisionType.Approved ? ApplicationStatus.Approved : ApplicationStatus.ApprovedWithConditions;
                var suffix = decision.Type == DecisionType.Approved ? "одобрено на запрошенных условиях." : "одобрено с изменением условий.";
                ChangeStatus(status, actor, decision.DecidedByUserId, $"{who}: {suffix}", now);
                var offer = Offer.Create(
                    Id,
                    decision.Id,
                    decision.ApprovedAmount ?? Amount,
                    decision.ApprovedTermMonths ?? TermMonths,
                    decision.InterestRate ?? Product.BaseRate,
                    decision.MonthlyPayment ?? LoanMath.MonthlyPayment(decision.ApprovedAmount ?? Amount, decision.InterestRate ?? Product.BaseRate, decision.ApprovedTermMonths ?? TermMonths),
                    now,
                    offerValidity);
                _offers.Add(offer);
                return offer;

            case DecisionType.Rejected:
                ChangeStatus(ApplicationStatus.Rejected, actor, decision.DecidedByUserId, $"{who}: отказ.", now);
                return null;

            case DecisionType.ManualReview:
                var reasons = string.Join(" ", decision.Reasons.Select(r => r.Title + "."));
                ChangeStatus(ApplicationStatus.ManualReview, ActorType.System, null, $"Передана менеджеру. {reasons}".Trim(), now);
                return null;

            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.Type, "Неизвестный тип решения.");
        }
    }

    // ---- Действия клиента по предложению ----

    public Offer AcceptOffer(Guid userId, DateTimeOffset now)
    {
        var offer = RequirePendingOffer(now);
        ChangeStatus(ApplicationStatus.ContractSigning, ActorType.Client, userId, "Клиент принял предложение банка.", now);
        offer.Accept(now);
        return offer;
    }

    public Offer DeclineOffer(Guid userId, string? reason, DateTimeOffset now)
    {
        var offer = RequirePendingOffer(now);
        ChangeStatus(ApplicationStatus.OfferDeclined, ActorType.Client, userId, string.IsNullOrWhiteSpace(reason) ? "Клиент отказался от предложения." : reason, now);
        offer.Decline(now);
        return offer;
    }

    // ---- Договор и выдача ----

    /// <summary>Прикрепляет сформированный договор после принятия предложения.</summary>
    public void AttachContract(LoanContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (Status != ApplicationStatus.ContractSigning)
        {
            throw new ConflictException("Договор формируется только после принятия предложения.");
        }

        _contracts.Add(contract);
    }

    /// <summary>Договор, который сейчас можно подписать; иначе 409 с объяснением.</summary>
    public LoanContract RequireSignableContract(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.ContractSigning)
        {
            throw new ConflictException($"Подписание доступно только в статусе «{ApplicationStatusCatalog.Get(ApplicationStatus.ContractSigning).Title}». Текущий статус: «{ApplicationStatusCatalog.Get(Status).Title}».");
        }

        var contract = CurrentContract ?? throw new ConflictException("Договор по заявке ещё не сформирован.");
        if (contract.IsSigningExpired(now))
        {
            throw new ConflictException($"Срок подписания договора истёк {contract.SigningDeadline:dd.MM.yyyy}. Подайте новую заявку.");
        }

        return contract;
    }

    /// <summary>Подписывает договор кодом подтверждения и переводит заявку в «Договор подписан».</summary>
    public LoanContract SignContract(string? code, Guid userId, string signerName, DateTimeOffset now)
    {
        var contract = RequireSignableContract(now);
        contract.Sign(code, userId, signerName, now);
        ChangeStatus(ApplicationStatus.Signed, ActorType.Client, userId, $"Договор {contract.Number} подписан простой электронной подписью.", now);
        return contract;
    }

    /// <summary>Отменяет заявку, если срок подписания договора истёк. Возвращает true при изменении.</summary>
    public bool ExpireContractIfNeeded(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.ContractSigning)
        {
            return false;
        }

        var contract = CurrentContract;
        if (contract is null || !contract.IsSigningExpired(now))
        {
            return false;
        }

        contract.Expire(now);
        ChangeStatus(ApplicationStatus.Cancelled, ActorType.System, null, $"Срок подписания договора {contract.Number} истёк.", now);
        return true;
    }

    /// <summary>Фиксирует попытку выдачи; успешная выдача завершает заявку статусом «Средства выданы».</summary>
    public void RecordDisbursement(Disbursement disbursement, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(disbursement);
        if (Status != ApplicationStatus.Signed)
        {
            throw new ConflictException($"Выдача возможна только по подписанному договору. Текущий статус: «{ApplicationStatusCatalog.Get(Status).Title}».");
        }

        _disbursements.Add(disbursement);
        if (disbursement.Status == DisbursementStatus.Completed)
        {
            ChangeStatus(
                ApplicationStatus.Disbursed,
                ActorType.System,
                null,
                $"Средства {Money.FormatRub(disbursement.Amount)} перечислены на расчётный счёт {disbursement.Account}, платёжное поручение {disbursement.PaymentOrderNumber}.",
                now);
        }
    }

    /// <summary>Переводит заявку в «предложение отклонено», если срок предложения истёк. Возвращает true при изменении.</summary>
    public bool ExpireOfferIfNeeded(DateTimeOffset now)
    {
        if (Status is not (ApplicationStatus.Approved or ApplicationStatus.ApprovedWithConditions))
        {
            return false;
        }

        var offer = CurrentOffer;
        if (offer is null || !offer.IsExpired(now))
        {
            return false;
        }

        offer.Expire(now);
        ChangeStatus(ApplicationStatus.OfferDeclined, ActorType.System, null, "Срок действия предложения истёк.", now);
        return true;
    }

    private Offer RequirePendingOffer(DateTimeOffset now)
    {
        if (Status is not (ApplicationStatus.Approved or ApplicationStatus.ApprovedWithConditions))
        {
            throw new ConflictException($"Предложение можно принять или отклонить только по одобренной заявке. Текущий статус: «{ApplicationStatusCatalog.Get(Status).Title}».");
        }

        var offer = CurrentOffer ?? throw new ConflictException("По заявке нет действующего предложения.");
        if (!offer.IsPending)
        {
            throw new ConflictException("Ответ на предложение уже дан.");
        }

        if (offer.IsExpired(now))
        {
            throw new ConflictException("Срок действия предложения истёк.");
        }

        return offer;
    }

    private static string? ActionName(ApplicationStatus target) => target switch
    {
        ApplicationStatus.Cancelled => "cancel",
        ApplicationStatus.Submitted => "submit",
        ApplicationStatus.Validating => "answerClarification",
        ApplicationStatus.ContractSigning => "acceptOffer",
        ApplicationStatus.OfferDeclined => "declineOffer",
        ApplicationStatus.Signed => "signContract",
        ApplicationStatus.NeedsClarification => "requestClarification",
        ApplicationStatus.Approved => "approve",
        ApplicationStatus.ApprovedWithConditions => "approveWithConditions",
        ApplicationStatus.Rejected => "reject",
        _ => null,
    };
}
