namespace AlfaCredit.Core.Domain.Users;

/// <summary>Роль пользователя в системе.</summary>
public enum UserRole
{
    /// <summary>Представитель клиента: собственник, директор, бухгалтер или уполномоченный сотрудник компании.</summary>
    Client = 1,

    /// <summary>Сотрудник банка: кредитный менеджер или андеррайтер.</summary>
    Manager = 2,

    /// <summary>Администратор системы: продукты, параметры политики, аудит.</summary>
    Admin = 3,
}
