using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Features.Financing;

namespace AlfaCredit.Api.Contracts.Financing;

/// <summary>Результат предварительного подбора: подходящие срочные продукты с расчётом, продукты с лимитом вне сравнения и продукты, которые не подходят, с причинами. Контракт: docs/financing-preview.md.</summary>
/// <param name="CalculatedAt">Момент расчёта (UTC); ответ не сохраняется.</param>
/// <param name="CalculationDate">Календарная дата расчёта по Москве: от неё считаются возраст бизнеса, дни до нужной даты и ориентиры сроков.</param>
/// <param name="Company">Компания из запроса.</param>
/// <param name="Request">Эхо запроса с названием цели и числом дней до нужной даты.</param>
/// <param name="Summary">Одна фраза для заголовка: сколько вариантов и какой рекомендован, либо почему вариантов нет.</param>
/// <param name="Disclaimer">Оговорка под расчётом, выводится как есть: расчёт не является одобрением и не гарантирует выдачу.</param>
/// <param name="Options">Подходящие продукты: сначала те, что успевают к дате, затем по возрастанию переплаты; первый помечен isRecommended.</param>
/// <param name="NotCompared">Продукты с лимитом (овердрафт, кредитная линия): по аннуитетной модели не сравниваются, оформляются отдельной заявкой.</param>
/// <param name="Unsuitable">Продукты, которые не подходят, с причинами по кодам.</param>
public sealed record FinancingPreviewDto(
    DateTimeOffset CalculatedAt,
    DateOnly CalculationDate,
    PreviewCompanyDto Company,
    PreviewRequestDto Request,
    string Summary,
    string Disclaimer,
    IReadOnlyList<FinancingOptionDto> Options,
    IReadOnlyList<NotComparedProductDto> NotCompared,
    IReadOnlyList<UnsuitableProductDto> Unsuitable);

/// <summary>Компания в подборе.</summary>
/// <param name="Id">Идентификатор компании.</param>
/// <param name="Name">Наименование.</param>
/// <param name="Inn">ИНН.</param>
/// <param name="BusinessAgeMonths">Полных месяцев с даты регистрации на дату расчёта.</param>
public sealed record PreviewCompanyDto(Guid Id, string Name, string Inn, int BusinessAgeMonths);

/// <summary>Эхо запроса.</summary>
/// <param name="Amount">Сумма, ₽.</param>
/// <param name="Purpose">Цель финансирования.</param>
/// <param name="PurposeTitle">Название цели для интерфейса.</param>
/// <param name="NeedByDate">К какой дате нужны деньги.</param>
/// <param name="DaysUntilNeeded">Дней от даты расчёта до needByDate.</param>
/// <param name="TermMonths">Срок из запроса; null, если не указан и подобран для каждого продукта.</param>
public sealed record PreviewRequestDto(decimal Amount, LoanPurpose Purpose, string PurposeTitle, DateOnly NeedByDate, int DaysUntilNeeded, int? TermMonths);

/// <summary>Объяснение, ограничение, требование или причина несоответствия: код для интерфейса и текст для клиента.</summary>
public sealed record PreviewNoteDto(string Code, string Text);

/// <summary>Подходящий продукт с расчётом стоимости, нагрузки и ориентиром получения денег.</summary>
/// <param name="ProductCode">Код продукта из GET /api/products.</param>
/// <param name="ProductName">Название продукта.</param>
/// <param name="ProductType">Тип продукта.</param>
/// <param name="IsRecommended">Первый в списке после сортировки.</param>
/// <param name="Amount">Сумма из запроса, ₽.</param>
/// <param name="TermMonths">Срок расчёта: из запроса или подобранный.</param>
/// <param name="TermSuggested">true, если срок не был указан и подобран системой.</param>
/// <param name="TermRange">Границы срока продукта для выбора другого срока.</param>
/// <param name="Cost">Стоимость: платёж, сумма выплат и переплата при базовой и верхней ставке.</param>
/// <param name="Affordability">Кредитная нагрузка («потяну ли платёж») или признак, что данных недостаточно.</param>
/// <param name="Funding">Ориентир получения денег при положительном решении («успею ли к дате»).</param>
/// <param name="SuggestedAmount">Меньшая сумма для повторного расчёта; null, если ограничений нет или данных недостаточно.</param>
/// <param name="Explanations">Почему вариант подходит: product_about, no_collateral, auto_decision_possible, term_suggested, data_prefilled, checks_automatic, cheapest.</param>
/// <param name="Limitations">Что может изменить условия: rate_depends_on_risk, manual_review_amount, collateral_required, amount_may_be_reduced, expense_ratio_high, affordability_unknown, elevated_debt_load, high_debt_load, deadline_missed.</param>
/// <param name="Requirements">Что действительно потребуется: financials_required, collateral_documents.</param>
/// <param name="ApplyWith">Готовое тело для POST /api/applications.</param>
public sealed record FinancingOptionDto(
    string ProductCode,
    string ProductName,
    ProductType ProductType,
    bool IsRecommended,
    decimal Amount,
    int TermMonths,
    bool TermSuggested,
    TermRangeDto TermRange,
    OptionCostDto Cost,
    AffordabilityDto Affordability,
    FundingEstimateDto Funding,
    AmountSuggestionDto? SuggestedAmount,
    IReadOnlyList<PreviewNoteDto> Explanations,
    IReadOnlyList<PreviewNoteDto> Limitations,
    IReadOnlyList<PreviewNoteDto> Requirements,
    ApplyWithDto ApplyWith);

/// <summary>Границы срока продукта, мес.</summary>
public sealed record TermRangeDto(int Min, int Max);

/// <summary>Стоимость при базовой ставке продукта и при верхней границе ставки автоматического одобрения (+3 п.п.).</summary>
/// <param name="InterestRate">Базовая ставка продукта, % годовых (грейд A).</param>
/// <param name="MaxInterestRate">Базовая ставка + 3 п.п. (грейд C).</param>
/// <param name="MonthlyPayment">Аннуитетный платёж при базовой ставке, ₽.</param>
/// <param name="MaxMonthlyPayment">Платёж при верхней границе ставки, ₽.</param>
/// <param name="TotalPayment">Сумма всех платежей по графику при базовой ставке, ₽.</param>
/// <param name="TotalInterest">Переплата при базовой ставке, ₽.</param>
/// <param name="MaxTotalInterest">Переплата при верхней границе ставки, ₽.</param>
public sealed record OptionCostDto(
    decimal InterestRate,
    decimal MaxInterestRate,
    decimal MonthlyPayment,
    decimal MaxMonthlyPayment,
    decimal TotalPayment,
    decimal TotalInterest,
    decimal MaxTotalInterest);

/// <summary>«Потяну ли платёж»: кредитная нагрузка по формуле сервиса скоринга.</summary>
/// <param name="MonthlyProfit">Месячная прибыль, по которой считается нагрузка, ₽; null, если данных недостаточно.</param>
/// <param name="CurrentDebtPayments">Текущие ежемесячные платежи по кредитам из карточки компании, ₽.</param>
/// <param name="DebtLoad">(текущие платежи + новый платёж) / месячная прибыль, не больше 9.99; null, если прибыли нет или данных недостаточно.</param>
/// <param name="Level">Comfortable (меньше 30 %), Moderate (30–50 %), Elevated (50–70 %: оценка риска снижается), High (70 % и больше или нет прибыли), InsufficientData (нет ни прибыли, ни расходов: оценка не выдаётся).</param>
/// <param name="ProfitBasis">NetProfit или RevenueMinusExpenses; null при InsufficientData.</param>
/// <param name="Explanation">Фраза для клиента.</param>
public sealed record AffordabilityDto(
    decimal? MonthlyProfit,
    decimal CurrentDebtPayments,
    decimal? DebtLoad,
    DebtLoadLevel Level,
    ProfitBasis? ProfitBasis,
    string Explanation);

/// <summary>«Успею ли к дате»: ориентир получения денег при положительном решении. Не является одобрением.</summary>
/// <param name="DecisionPath">Auto: при чистых проверках и хорошей оценке решение без менеджера; ManualReview: заявку в любом случае рассматривает менеджер.</param>
/// <param name="BusinessDays">Рабочих дней от даты расчёта до денег на счёте при положительном решении.</param>
/// <param name="ExpectedDate">Ориентировочная дата получения денег.</param>
/// <param name="InTime">Ориентир не позже нужной даты.</param>
/// <param name="Explanation">Фраза для клиента: почему такой срок и успеваем ли.</param>
public sealed record FundingEstimateDto(FundingPath DecisionPath, int BusinessDays, DateOnly ExpectedDate, bool InTime, string Explanation);

/// <summary>Меньшая сумма для повторного расчёта.</summary>
/// <param name="Amount">Сумма, ₽: наименьшая из действующих границ, округлена вниз до 10 000 ₽ для границы по нагрузке.</param>
/// <param name="Basis">ProductMaximum (максимум продукта), AutoApprovalLimit (лимит автоматического решения), RevenueShare (доля годовой выручки по политике), DebtLoad (платёж без штрафа за нагрузку).</param>
/// <param name="Text">Обоснование для клиента.</param>
public sealed record AmountSuggestionDto(decimal Amount, AmountSuggestionBasis Basis, string Text);

/// <summary>Тело POST /api/applications для выбранного варианта: отправляется без изменений.</summary>
public sealed record ApplyWithDto(Guid CompanyId, string ProductCode, decimal Amount, int TermMonths, LoanPurpose Purpose, DateOnly NeedByDate);

/// <summary>Продукт, который не подходит, с причинами: purpose_not_allowed, amount_below_min, amount_above_max, term_out_of_range, business_too_young, product_inactive.</summary>
/// <param name="ProductCode">Код продукта.</param>
/// <param name="ProductName">Название продукта.</param>
/// <param name="Reasons">Причины по кодам.</param>
/// <param name="SuggestedAmount">Максимальная сумма продукта для повторного расчёта, если мешает только сумма; иначе null.</param>
public sealed record UnsuitableProductDto(string ProductCode, string ProductName, IReadOnlyList<PreviewNoteDto> Reasons, AmountSuggestionDto? SuggestedAmount);

/// <summary>Продукт с лимитом вне сравнения: овердрафт, кредитная линия.</summary>
/// <param name="ProductCode">Код продукта.</param>
/// <param name="ProductName">Название продукта.</param>
/// <param name="ProductType">Тип продукта.</param>
/// <param name="Reason">Код limit_product и объяснение для клиента.</param>
public sealed record NotComparedProductDto(string ProductCode, string ProductName, ProductType ProductType, PreviewNoteDto Reason);
