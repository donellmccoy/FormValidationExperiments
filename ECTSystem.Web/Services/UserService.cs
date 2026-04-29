using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ECTSystem.Web.Services;

/// <summary>
/// Resolves opaque Identity user IDs (GUIDs) into human-readable display names by calling
/// <c>api/user/lookup</c> on the API and caching the responses in-process for the lifetime
/// of the Blazor WebAssembly session.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetDisplayNamesAsync"/> batches all uncached IDs into a single request using
/// repeated <c>ids=</c> query parameters; this is intentional so that grids displaying a
/// page of cases can resolve every <c>CheckedOutBy</c> / <c>CreatedBy</c> author with one
/// HTTP round-trip rather than N. IDs are URL-encoded with
/// <see cref="Uri.EscapeDataString(string)"/>.
/// </para>
/// <para>
/// <strong>Cache lifetime:</strong> the in-memory dictionary lives for the lifetime of the
/// service instance and is never evicted. This is acceptable today because the service is
/// scoped per circuit and display-name churn is low, but it means a renamed user will
/// continue to display their old name until the user reloads. Replacing this with a
/// size-bounded <c>IMemoryCache</c> with sliding expiration is tracked as a follow-up.
/// </para>
/// <para>
/// <strong>Failure semantics:</strong> when the lookup call fails for reasons other than
/// cancellation, the exception is logged and rethrown so callers can decide whether to
/// degrade gracefully. The fallback in the returned dictionary is the raw user ID itself,
/// which keeps grids legible even if the lookup partially succeeds.
/// </para>
/// </remarks>
public class UserService(HttpClient httpClient, ILogger<UserService> logger) : IUserService
{
    private readonly Dictionary<string, string> _cache = new();

    public async Task<Dictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var uncached = userIds
            .Where(id => !string.IsNullOrEmpty(id) && !_cache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (uncached.Count > 0)
        {
            var query = string.Join("&", uncached.Select(id => $"ids={Uri.EscapeDataString(id)}"));
            try
            {
                var result = await httpClient.GetFromJsonAsync<Dictionary<string, string>>(
                    $"api/user/lookup?{query}", cancellationToken);

                if (result is not null)
                {
                    foreach (var kvp in result)
                    {
                        _cache[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to look up display names for {Count} user IDs", uncached.Count);
                throw;
            }
        }

        return userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToDictionary(id => id, id => _cache.GetValueOrDefault(id, id));
    }

    public async Task<string> GetDisplayNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return string.Empty;

        var names = await GetDisplayNamesAsync([userId], cancellationToken);
        return names.GetValueOrDefault(userId, userId);
    }
}
