using System.Security.Claims;

namespace ECTSystem.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated user's identifier (NameIdentifier claim) or throws
    /// <see cref="UnauthorizedAccessException"/> when the claim is missing.
    /// </summary>
    public static string GetRequiredUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing NameIdentifier claim.");
}
