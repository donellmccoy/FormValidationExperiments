using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ECTSystem.Web.Services;

/// <summary>
/// Caches the authenticated user's server-assigned identity (user ID) so that
/// client-side code can compare it with <c>CheckedOutBy</c> on LOD cases.
/// Required because ASP.NET Core Identity Data Protection bearer tokens are opaque —
/// the Blazor WASM client cannot parse claims from them.
/// </summary>
/// <remarks>
/// <para>
/// Concurrency is handled with a double-checked <see cref="SemaphoreSlim"/> so that two
/// components racing to call <see cref="GetUserIdAsync"/> on first use share a single
/// <c>api/user/me</c> request rather than issuing duplicates. The double-check is required:
/// the cheap unsynchronized read short-circuits the steady-state path, and the second
/// read inside the lock prevents a second waiter from re-fetching after the first writer
/// already populated the field.
/// </para>
/// <para>
/// <see cref="Clear"/> is called from <c>AuthService.LogoutAsync</c> to discard the cached
/// ID at logout; without this, a subsequent login on the same circuit would surface the
/// previous user's ID until a hard reload.
/// </para>
/// </remarks>
public class CurrentUserService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CurrentUserService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private string _userId;

    public CurrentUserService(IHttpClientFactory httpClientFactory, ILogger<CurrentUserService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch current user id from /api/user/me; treating as unauthenticated.");
            }

            return _userId;
        }
        finally
        {
            _initLock.Release();
        }
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
