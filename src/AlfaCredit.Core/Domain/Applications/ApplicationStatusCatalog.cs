namespace AlfaCredit.Core.Domain.Applications;

/// <summary>Описание статуса для интерфейса: что происходит и что делать дальше клиенту и банку.</summary>
/// <param name="Code">Код статуса.</param>
/// <param name="Title">Короткое название.</param>
/// <param name="ClientDescription">Что происходит, простым языком для клиента.</param>
/// <param name="ClientNextAction">Что сейчас требуется от клиента.</param>
/// <param name="BankNextAction">Что сейчас требуется от банка.</param>
/// <param name="IsTerminal">Конечный статус, дальнейшие переходы невозможны.</param>
/// <param name="ProgressStep">Шаг прогресс-бара от 0 до <see cref="ApplicationStatusCatalog.TotalProgressSteps"/>.</param>
public sealed record ApplicationStatusInfo(
    ApplicationStatus Code,
    string Title,
    string ClientDescription,
    string ClientNextAction,
    string BankNextAction,
    bool IsTerminal,
    int ProgressStep);

public static class ApplicationStatusCatalog
{
    public const int TotalProgressSteps = 6;

    private static readonly IReadOnlyDictionary<ApplicationStatus, ApplicationStatusInfo> Items = new[]
    {
        new ApplicationStatusInfo(
            ApplicationStatus.Draft,
            "Черновик",
            "Заявка сохранена, но ещё не отправлена в банк.",
            "Заполните данные и отправьте заявку.",
            "Ожидается отправка заявки клиентом.",
            IsTerminal: false,
            ProgressStep: 0),
        new ApplicationStatusInfo(
            ApplicationStatus.Submitted,
            "Отправлена",
            "Заявка принята банком и ожидает автоматической проверки.",
            "Ничего делать не нужно: проверка начнётся автоматически.",
            "Ожидает автоматической проверки.",
            IsTerminal: false,
            ProgressStep: 1),
        new ApplicationStatusInfo(
            ApplicationStatus.Validating,
            "Проверка данных",
            "Банк проверяет полноту и корректность данных заявки.",
            "Ожидайте результатов проверки.",
            "Выполняется автоматически.",
            IsTerminal: false,
            ProgressStep: 2),
        new ApplicationStatusInfo(
            ApplicationStatus.NeedsClarification,
            "Нужны уточнения",
            "Банку не хватает данных или документов, чтобы продолжить рассмотрение.",
            "Ответьте на запрос банка и приложите недостающие документы.",
            "Ожидается ответ клиента на запрос.",
            IsTerminal: false,
            ProgressStep: 2),
        new ApplicationStatusInfo(
            ApplicationStatus.Enriching,
            "Сбор сведений",
            "Банк получает сведения о компании из внешних источников: ЕГРЮЛ, ФНС, бюро кредитных историй.",
            "Ожидайте, обычно это занимает несколько минут.",
            "Выполняется автоматически.",
            IsTerminal: false,
            ProgressStep: 3),
        new ApplicationStatusInfo(
            ApplicationStatus.Scoring,
            "Оценка риска",
            "Рассчитывается скоринговая оценка по данным заявки и внешних проверок.",
            "Ожидайте результата оценки.",
            "Выполняется автоматически.",
            IsTerminal: false,
            ProgressStep: 3),
        new ApplicationStatusInfo(
            ApplicationStatus.Deciding,
            "Принятие решения",
            "К заявке применяется кредитная политика банка.",
            "Ожидайте решения.",
            "Выполняется автоматически.",
            IsTerminal: false,
            ProgressStep: 3),
        new ApplicationStatusInfo(
            ApplicationStatus.ManualReview,
            "Рассмотрение менеджером",
            "Заявку изучает кредитный менеджер банка.",
            "Ожидайте решения менеджера. Возможен запрос уточнений.",
            "Изучите заявку, запросите уточнения или примите решение.",
            IsTerminal: false,
            ProgressStep: 3),
        new ApplicationStatusInfo(
            ApplicationStatus.Approved,
            "Одобрена",
            "Банк готов предоставить финансирование на запрошенных условиях.",
            "Ознакомьтесь с предложением и примите или отклоните его.",
            "Ожидается ответ клиента на предложение.",
            IsTerminal: false,
            ProgressStep: 4),
        new ApplicationStatusInfo(
            ApplicationStatus.ApprovedWithConditions,
            "Одобрена с изменением условий",
            "Банк готов предоставить финансирование, но сумма, срок или ставка отличаются от запрошенных.",
            "Ознакомьтесь с новыми условиями и примите или отклоните предложение.",
            "Ожидается ответ клиента на предложение.",
            IsTerminal: false,
            ProgressStep: 4),
        new ApplicationStatusInfo(
            ApplicationStatus.Rejected,
            "Отказ",
            "Банк не может предоставить финансирование по этой заявке. Причины указаны в решении.",
            "Вы можете подать новую заявку позже или выбрать другой продукт.",
            "Рассмотрение завершено.",
            IsTerminal: true,
            ProgressStep: 4),
        new ApplicationStatusInfo(
            ApplicationStatus.ContractSigning,
            "Подписание договора",
            "Договор сформирован и ожидает подписания простой электронной подписью.",
            "Проверьте договор, запросите код подтверждения и введите его в кабинете.",
            "Ожидается подпись клиента.",
            IsTerminal: false,
            ProgressStep: 5),
        new ApplicationStatusInfo(
            ApplicationStatus.Signed,
            "Договор подписан",
            "Договор подписан, банк переводит средства на расчётный счёт компании.",
            "Ожидайте зачисления на расчётный счёт, обычно это занимает несколько секунд.",
            "Выполняется выдача средств.",
            IsTerminal: false,
            ProgressStep: 5),
        new ApplicationStatusInfo(
            ApplicationStatus.Disbursed,
            "Средства выданы",
            "Деньги перечислены на расчётный счёт компании.",
            "Финансирование получено.",
            "Сделка завершена.",
            IsTerminal: true,
            ProgressStep: 6),
        new ApplicationStatusInfo(
            ApplicationStatus.Cancelled,
            "Отменена",
            "Заявка отменена: клиент отозвал её или истёк срок подписания договора.",
            "Вы можете создать новую заявку.",
            "Рассмотрение прекращено.",
            IsTerminal: true,
            ProgressStep: 0),
        new ApplicationStatusInfo(
            ApplicationStatus.OfferDeclined,
            "Предложение отклонено",
            "Клиент отказался от предложенных условий или срок предложения истёк.",
            "Вы можете подать новую заявку.",
            "Рассмотрение завершено.",
            IsTerminal: true,
            ProgressStep: 4),
    }.ToDictionary(info => info.Code);

    public static IReadOnlyCollection<ApplicationStatusInfo> All => Items.Values.ToArray();

    public static ApplicationStatusInfo Get(ApplicationStatus status) =>
        Items.TryGetValue(status, out var info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(status), status, "Статус не описан в каталоге.");
}
