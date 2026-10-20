using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Core.Features.Applications;

public sealed record ApplicationListQuery(ApplicationStatus? Status, int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public int NormalizedPage => Math.Max(1, Page);

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
