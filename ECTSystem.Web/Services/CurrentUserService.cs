using System.Net.Http.Json;
using ECTSystem.Shared.ViewModels;
using Microsoft.Extensions.Logging;

namespace ECTSystem.Web.Services;

/// <summary>
/// Caches the authenticated user's server-assigned identity (user ID, full name, role)
/// so that client-side code can compare it with <c>CheckedOutBy</c> on LOD cases and
/// surface the user's display name + role in the page header.
/// Required because ASP.NET Core Identity Data Protection bearer tokens are opaque —
/// the Blazor WASM client cannot parse claims from them.
/// </summary>
/// <remarks>
/// <para>
/// Concurrency is handled with a double-checked <see cref="SemaphoreSlim"/> so that two
/// components racing to call <see cref="GetUserInfoAsync"/> on first use share a single
/// <c>api/user/me</c> request rather than issuing duplicates. The double-check is required:
/// the cheap unsynchronized read short-circuits the steady-state path, and the second
/// read inside the lock prevents a second waiter from re-fetching after the first writer
/// already populated the field.
/// </para>
/// <para>
/// <see cref="Clear"/> is called from <c>AuthService.LogoutAsync</c> to discard the cached
/// data at logout; without this, a subsequent login on the same circuit would surface the
/// previous user's identity until a hard reload.
/// </para>
/// </remarks>
public class CurrentUserService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CurrentUserService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private CurrentUserDto _userInfo;

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
        var info = await GetUserInfoAsync().ConfigureAwait(false);
        return info?.UserId;
    }

    /// <summary>
    /// Gets the current user's identity info (user ID, display name, full name, role),
    /// fetching it from the API if not yet cached. Returns <c>null</c> if the API call
    /// fails (e.g., not authenticated).
    /// </summary>
    public async Task<CurrentUserDto> GetUserInfoAsync()
    {
        if (_userInfo is not null)
        {
            return _userInfo;
        }

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_userInfo is not null)
            {
                return _userInfo;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("Api");
                _userInfo = await client.GetFromJsonAsync<CurrentUserDto>("api/user/me");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP failure fetching current user info from /api/user/me; treating as unauthenticated.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout fetching current user info from /api/user/me; treating as unauthenticated.");
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "Malformed response from /api/user/me; treating as unauthenticated.");
            }

            return _userInfo;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Clears the cached user info (e.g., on logout).
    /// </summary>
    public void Clear() => _userInfo = null;
}
