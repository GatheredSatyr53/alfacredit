using System.Security.Claims;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AppClaimTypes.Subject);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
    }

    public static UserRole GetRole(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AppClaimTypes.Role);
        return Enum.TryParse<UserRole>(value, ignoreCase: false, out var role)
            ? role
            : throw new InvalidOperationException("Authenticated principal has no valid 'role' claim.");
    }

    public static Actor ToActor(this ClaimsPrincipal principal) => new(principal.GetUserId(), principal.GetRole());
}
