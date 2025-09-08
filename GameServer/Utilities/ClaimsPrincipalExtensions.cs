using System.Security.Claims;

namespace GameServer.Utilities
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal principal)
        {
            if (principal == null)
                return null;
            var sub = principal.FindFirst("sub")?.Value;
            if (!string.IsNullOrWhiteSpace(sub))
                return sub;
            var nameId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(nameId))
                return nameId;
            return null;
        }
    }
}
