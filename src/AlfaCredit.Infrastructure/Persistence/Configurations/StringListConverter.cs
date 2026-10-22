using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

/// <summary>Списки кодов хранятся строкой через запятую: читаемо в базе, без отдельных таблиц.</summary>
internal static class StringListConverter
{
    public static readonly ValueConverter<List<string>, string> Converter = new(
        list => string.Join(',', list),
        value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());

    public static readonly ValueComparer<List<string>> Comparer = new(
        (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());
}
