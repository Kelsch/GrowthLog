using System.Security.Claims;

namespace GrowthLog.Web.Data.Authorization;

public static class CurrentUserExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.NameIdentifier);
}
