using AlfaCredit.Core.Domain.Companies;

namespace AlfaCredit.Api.Contracts.Companies;

/// <summary>Компания или ИП клиента.</summary>
/// <param name="Id">Идентификатор; передаётся в заявке как companyId.</param>
/// <param name="Inn">ИНН.</param>
/// <param name="Ogrn">ОГРН или ОГРНИП.</param>
/// <param name="Name">Наименование.</param>
/// <param name="LegalForm">Организационно-правовая форма.</param>
/// <param name="Okved">Основной ОКВЭД.</param>
/// <param name="RegisteredAt">Дата регистрации.</param>
/// <param name="BusinessAgeMonths">Возраст бизнеса в месяцах на сегодня.</param>
/// <param name="Address">Адрес.</param>
/// <param name="HeadName">Руководитель.</param>
/// <param name="TaxRegime">Режим налогообложения.</param>
/// <param name="AnnualRevenue">Выручка за 12 месяцев, ₽.</param>
/// <param name="MonthlyDebtPayments">Текущие ежемесячные платежи по обязательствам, ₽.</param>
/// <param name="EmployeesCount">Число сотрудников.</param>
/// <param name="MyRole">Роль текущего пользователя в компании; пусто для сотрудников банка.</param>
public sealed record CompanyDto(
    Guid Id,
    string Inn,
    string Ogrn,
    string Name,
    LegalForm LegalForm,
    string Okved,
    DateOnly RegisteredAt,
    int BusinessAgeMonths,
    string Address,
    string HeadName,
    TaxRegime TaxRegime,
    decimal AnnualRevenue,
    decimal MonthlyDebtPayments,
    int EmployeesCount,
    CompanyMemberRole? MyRole);
