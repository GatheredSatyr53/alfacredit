using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Domain.Applications;

/// <summary>Кредитная заявка: агрегат, через который проходит весь путь от подачи до выдачи.</summary>
public sealed class CreditApplication
{
    public const int NumberMaxLength = 32;
    public const int PurposeDescriptionMaxLength = 1000;

    private readonly List<ApplicationStatusHistory> _history = [];

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

    public void Cancel(Guid userId, string? reason, DateTimeOffset now) =>
        ChangeStatus(
            ApplicationStatus.Cancelled,
            ActorType.Client,
            userId,
            string.IsNullOrWhiteSpace(reason) ? "Заявка отменена клиентом." : reason,
            now);

    /// <summary>Действия, доступные участнику в текущем статусе (ключи для кнопок интерфейса).</summary>
    public IReadOnlyList<string> AvailableActions(ActorType actor) =>
        ApplicationWorkflow.NextStatuses(Status, actor)
            .Select(ActionName)
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct()
            .ToArray();

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
