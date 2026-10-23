using System.Globalization;
using System.Net;
using System.Text;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Contracts;

/// <summary>Печатная форма договора: самодостаточный HTML без внешних ресурсов, пригодный для печати и сохранения.</summary>
public static class ContractDocumentRenderer
{
    // Собственный формат чисел: не зависит от наличия ICU в контейнере и даёт «1 500 000,00»
    private static readonly NumberFormatInfo Ru = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
    };

    public static string Render(CreditApplication application, LoanContract contract, IReadOnlyList<ScheduleRow> schedule)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(schedule);

        var company = application.Company;
        var sb = new StringBuilder(16 * 1024);
        sb.Append("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(E($"Кредитный договор {contract.Number}")).Append("</title>");
        sb.Append("<style>body{font-family:Georgia,'Times New Roman',serif;max-width:860px;margin:32px auto;padding:0 16px;color:#111;line-height:1.45}");
        sb.Append("h1{font-size:22px;text-align:center;margin-bottom:4px}h2{font-size:16px;margin-top:28px}.muted{color:#555;text-align:center;font-size:13px}");
        sb.Append("table{border-collapse:collapse;width:100%;margin-top:8px;font-size:14px}th,td{border:1px solid #bbb;padding:6px 8px;text-align:left}th{background:#f3f3f3}");
        sb.Append("td.num,th.num{text-align:right;white-space:nowrap}.sign{margin-top:32px;padding:12px 16px;border:1px dashed #888}.demo{margin-top:24px;font-size:12px;color:#666}");
        sb.Append("@media print{body{margin:0}.demo{display:none}}</style></head><body>");

        sb.Append("<h1>").Append(E($"Кредитный договор № {contract.Number}")).Append("</h1>");
        sb.Append("<p class=\"muted\">").Append(E($"Сформирован {Date(contract.CreatedAt)} по заявке {application.Number}")).Append("</p>");

        sb.Append("<h2>1. Стороны</h2><p>");
        sb.Append("<b>Кредитор:</b> ").Append(E(contract.LenderName)).Append(".<br>");
        sb.Append("<b>Заёмщик:</b> ").Append(E(company.Name)).Append(", ИНН ").Append(E(company.Inn)).Append(", ОГРН ").Append(E(company.Ogrn))
          .Append(", адрес: ").Append(E(company.Address)).Append(", в лице ").Append(E(company.HeadName)).Append(".</p>");

        sb.Append("<h2>2. Условия кредита</h2><table>");
        Row(sb, "Продукт", $"{application.Product.Name} ({application.Product.Code})");
        Row(sb, "Цель", application.Purpose.Title());
        Row(sb, "Сумма кредита", Rub(contract.Amount));
        Row(sb, "Срок", $"{contract.TermMonths} мес.");
        Row(sb, "Процентная ставка", $"{contract.InterestRate.ToString("0.##", CultureInfo.InvariantCulture)} % годовых");
        Row(sb, "Ежемесячный платёж", Rub(contract.MonthlyPayment));
        Row(sb, "Сумма всех платежей", Rub(contract.TotalPayment));
        Row(sb, "Переплата (проценты за весь срок)", Rub(contract.TotalInterest));
        Row(sb, "Счёт для зачисления", contract.DisbursementAccount);
        Row(sb, "Срок подписания", $"до {Date(contract.SigningDeadline)}");
        sb.Append("</table>");

        sb.Append("<h2>3. Порядок погашения</h2><p>Погашение аннуитетными платежами ежемесячно по графику. Проценты начисляются на остаток основного долга. Досрочное погашение допускается без комиссий.</p>");
        sb.Append("<table><tr><th>№</th><th>Дата платежа</th><th class=\"num\">Платёж</th><th class=\"num\">Основной долг</th><th class=\"num\">Проценты</th><th class=\"num\">Остаток долга</th></tr>");
        foreach (var row in schedule)
        {
            sb.Append("<tr><td>").Append(row.Number).Append("</td><td>").Append(E(row.DueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))).Append("</td>");
            sb.Append("<td class=\"num\">").Append(E(Rub(row.Payment))).Append("</td><td class=\"num\">").Append(E(Rub(row.Principal))).Append("</td>");
            sb.Append("<td class=\"num\">").Append(E(Rub(row.Interest))).Append("</td><td class=\"num\">").Append(E(Rub(row.Balance))).Append("</td></tr>");
        }

        sb.Append("</table><p class=\"muted\">").Append(E(schedule.Count > 0 && application.CurrentDisbursement is null
            ? "Даты платежей ориентировочные: отсчёт начнётся от даты выдачи средств."
            : "Даты платежей рассчитаны от даты выдачи средств.")).Append("</p>");

        sb.Append("<h2>4. Подписи сторон</h2><div class=\"sign\">");
        if (contract.Status == ContractStatus.Signed)
        {
            sb.Append("<p><b>Заёмщик:</b> ").Append(E(contract.SignedByName ?? string.Empty)).Append(", ").Append(E(company.Name)).Append("<br>");
            sb.Append("Подписано ").Append(E(DateTime(contract.SignedAt!.Value))).Append(" (UTC).<br>");
            sb.Append("Способ подписания: ").Append(E(contract.SignatureMethod ?? LoanContract.SimpleSignatureMethod)).Append(".</p>");
            sb.Append("<p><b>Кредитор:</b> ").Append(E(contract.LenderName)).Append(", подписано автоматически при подписании заёмщиком.</p>");
        }
        else
        {
            sb.Append("<p><b>Заёмщик:</b> ").Append(E(company.HeadName)).Append(", ").Append(E(company.Name)).Append(" — договор ожидает подписания кодом подтверждения в кабинете.</p>");
            sb.Append("<p><b>Кредитор:</b> ").Append(E(contract.LenderName)).Append(".</p>");
        }

        var disbursement = application.CurrentDisbursement;
        if (disbursement is { Status: DisbursementStatus.Completed })
        {
            sb.Append("<p><b>Выдача:</b> ").Append(E($"{Rub(disbursement.Amount)} перечислены на счёт {disbursement.Account} {DateTime(disbursement.CompletedAt ?? disbursement.RequestedAt)} (UTC), платёжное поручение {disbursement.PaymentOrderNumber}.")).Append("</p>");
        }

        sb.Append("</div>");
        sb.Append("<p class=\"demo\">Демонстрационный документ прототипа AlfaCredit: стороны, реквизиты и выдача смоделированы, документ не является офертой или договором.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string name, string value) =>
        sb.Append("<tr><th>").Append(E(name)).Append("</th><td>").Append(E(value)).Append("</td></tr>");

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static string Rub(decimal value) => value.ToString("#,0.00", Ru).Replace(' ', ' ') + " ₽";

    private static string Date(DateTimeOffset value) => value.ToUniversalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string DateTime(DateTimeOffset value) => value.ToUniversalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
}
