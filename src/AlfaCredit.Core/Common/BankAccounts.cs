using AlfaCredit.Core.Domain.Companies;

namespace AlfaCredit.Core.Common;

/// <summary>Синтетические реквизиты: расчётный счёт компании выводится из ИНН детерминированно, как и данные симуляторов.</summary>
public static class BankAccounts
{
    /// <summary>20-значный расчётный счёт: 40702 для юрлиц, 40802 для ИП, валюта 810, далее цифры из хеша ИНН.</summary>
    public static string SettlementAccountFor(string inn, LegalForm legalForm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inn);
        var prefix = legalForm == LegalForm.Ip ? "40802810" : "40702810";
        var digits = new char[12];
        var hash = Fnv1a(inn);
        for (var i = 0; i < digits.Length; i++)
        {
            digits[i] = (char)('0' + (int)(hash % 10));
            hash = hash / 10 == 0 ? Fnv1a(inn + i) : hash / 10;
        }

        return prefix + new string(digits);
    }

    private static ulong Fnv1a(string text)
    {
        unchecked
        {
            var hash = 14695981039346656037ul;
            foreach (var ch in text)
            {
                hash = (hash ^ ch) * 1099511628211ul;
            }

            return hash;
        }
    }
}
