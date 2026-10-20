namespace AlfaCredit.Core.Domain.Companies;

/// <summary>Компания или ИП, от имени которых подаются кредитные заявки.</summary>
public sealed class Company
{
    public const int InnMaxLength = 12;
    public const int OgrnMaxLength = 15;
    public const int NameMaxLength = 256;
    public const int OkvedMaxLength = 16;
    public const int AddressMaxLength = 512;
    public const int HeadNameMaxLength = 256;

    private readonly List<CompanyMember> _members = [];

    private Company()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>ИНН: 10 цифр у организаций, 12 у ИП. Уникален.</summary>
    public string Inn { get; private set; } = string.Empty;

    /// <summary>ОГРН (13 цифр) или ОГРНИП (15 цифр).</summary>
    public string Ogrn { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public LegalForm LegalForm { get; private set; }

    /// <summary>Основной код ОКВЭД.</summary>
    public string Okved { get; private set; } = string.Empty;

    public DateOnly RegisteredAt { get; private set; }

    public string Address { get; private set; } = string.Empty;

    /// <summary>Руководитель (для ИП совпадает с предпринимателем).</summary>
    public string HeadName { get; private set; } = string.Empty;

    public TaxRegime TaxRegime { get; private set; }

    /// <summary>Выручка за последние 12 месяцев, ₽.</summary>
    public decimal AnnualRevenue { get; private set; }

    /// <summary>Расходы за последние 12 месяцев, ₽. Пусто, если клиент не ведёт учёт расходов (например, патент).</summary>
    public decimal? AnnualExpenses { get; private set; }

    /// <summary>Чистая прибыль за последние 12 месяцев, ₽. Может быть отрицательной (убыток) или пустой.</summary>
    public decimal? NetProfit { get; private set; }

    /// <summary>Текущие ежемесячные платежи по кредитам и лизингу, ₽.</summary>
    public decimal MonthlyDebtPayments { get; private set; }

    public int EmployeesCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<CompanyMember> Members => _members.AsReadOnly();

    public static Company Create(
        string inn,
        string ogrn,
        string name,
        LegalForm legalForm,
        string okved,
        DateOnly registeredAt,
        string address,
        string headName,
        TaxRegime taxRegime,
        decimal annualRevenue,
        decimal? annualExpenses,
        decimal? netProfit,
        decimal monthlyDebtPayments,
        int employeesCount,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inn);
        ArgumentException.ThrowIfNullOrWhiteSpace(ogrn);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(headName);
        ArgumentOutOfRangeException.ThrowIfNegative(annualRevenue);
        if (annualExpenses is { } expenses)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(expenses);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(monthlyDebtPayments);
        ArgumentOutOfRangeException.ThrowIfNegative(employeesCount);

        var normalizedInn = inn.Trim();
        if (normalizedInn.Length is not (10 or 12) || !normalizedInn.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("ИНН должен состоять из 10 или 12 цифр.", nameof(inn));
        }

        return new Company
        {
            Id = Guid.CreateVersion7(),
            Inn = normalizedInn,
            Ogrn = ogrn.Trim(),
            Name = name.Trim(),
            LegalForm = legalForm,
            Okved = okved.Trim(),
            RegisteredAt = registeredAt,
            Address = address.Trim(),
            HeadName = headName.Trim(),
            TaxRegime = taxRegime,
            AnnualRevenue = annualRevenue,
            AnnualExpenses = annualExpenses,
            NetProfit = netProfit,
            MonthlyDebtPayments = monthlyDebtPayments,
            EmployeesCount = employeesCount,
            CreatedAt = createdAt,
        };
    }

    public CompanyMember AddMember(Guid userId, CompanyMemberRole role, DateTimeOffset joinedAt)
    {
        var existing = _members.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null)
        {
            return existing;
        }

        var member = CompanyMember.Create(Id, userId, role, joinedAt);
        _members.Add(member);
        return member;
    }

    public bool HasMember(Guid userId) => _members.Any(m => m.UserId == userId);

    /// <summary>Код региона по первым двум цифрам ИНН.</summary>
    public string RegionCode => Inn[..2];

    /// <summary>Полных месяцев с даты регистрации на указанную дату.</summary>
    public int GetBusinessAgeMonths(DateOnly today)
    {
        var months = (today.Year - RegisteredAt.Year) * 12 + today.Month - RegisteredAt.Month;
        if (today.Day < RegisteredAt.Day)
        {
            months--;
        }

        return Math.Max(0, months);
    }
}
