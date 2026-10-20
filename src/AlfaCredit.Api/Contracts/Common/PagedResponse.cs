using AlfaCredit.Core.Common;

namespace AlfaCredit.Api.Contracts.Common;

/// <summary>Страница результатов.</summary>
/// <param name="Items">Элементы текущей страницы.</param>
/// <param name="Page">Номер страницы, с 1.</param>
/// <param name="PageSize">Размер страницы.</param>
/// <param name="TotalCount">Всего элементов по фильтру.</param>
/// <param name="TotalPages">Всего страниц.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagedResponse<T> From<TSource>(PagedResult<TSource> result, Func<TSource, T> map) =>
        new(result.Items.Select(map).ToArray(), result.Page, result.PageSize, result.TotalCount, result.TotalPages);
}
