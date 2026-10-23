using System.Security.Cryptography;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Contracts;

namespace AlfaCredit.Core.Features.Contracts;

/// <summary>Шестизначный код подтверждения из криптографического генератора.</summary>
public sealed class RandomSigningCodeGenerator : ISigningCodeGenerator
{
    public string Next() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString($"D{LoanContract.CodeLength}", System.Globalization.CultureInfo.InvariantCulture);
}
