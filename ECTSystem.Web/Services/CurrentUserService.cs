using System.Net.Http.Json;

namespace ECTSystem.Web.Services;

/// <summary>
/// Caches the authenticated user's server-assigned identity (user ID) so that
/// client-side code can compare it with <c>CheckedOutBy</c> on LOD cases.
/// Required because ASP.NET Core Identity Data Protection bearer tokens are opaque —
/// the Blazor WASM client cannot parse claims from them.
/// </summary>
public class CurrentUserService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private string _userId;

    public CurrentUserService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Gets the current user's server-assigned ID, fetching it from the API if not yet cached.
    /// Returns <c>null</c> if the API call fails (e.g., not authenticated).
    /// </summary>
    public async Task<string> GetUserIdAsync()
    {
        if (_userId is not null)
        {
            return _userId;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetFromJsonAsync<UserInfo>("api/user/me");
            _userId = response?.UserId;
        }
        catch
        {
            // Not authenticated or API unavailable — return null
        }

        return _userId;
    }

    /// <summary>
    /// Clears the cached user ID (e.g., on logout).
    /// </summary>
    public void Clear() => _userId = null;

    private class UserInfo
    {
        public string UserId { get; set; }
        public string Name { get; set; }
    }
}
