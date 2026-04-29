using System.Collections.Concurrent;
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
/// <strong>Cache lifetime:</strong> entries are stored in a thread-safe sliding-expiration
/// dictionary keyed by user ID. Each access refreshes the entry's expiry; entries that
/// have not been touched within <see cref="CacheSlidingTtl"/> are evicted lazily on the
/// next batched lookup. This bounds staleness so a renamed user surfaces within the TTL
/// rather than persisting for the full circuit lifetime.
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
    private static readonly TimeSpan CacheSlidingTtl = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    private bool TryGetFresh(string id, DateTimeOffset now, out string name)
    {
        if (_cache.TryGetValue(id, out var entry) && entry.Expiry > now)
        {
            entry.Expiry = now + CacheSlidingTtl;
            name = entry.Name;
            return true;
        }
        name = null;
        return false;
    }

    public async Task<Dictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var requested = userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var uncached = requested
            .Where(id => !TryGetFresh(id, now, out _))
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
                    var expiry = DateTimeOffset.UtcNow + CacheSlidingTtl;
                    foreach (var kvp in result)
                    {
                        _cache[kvp.Key] = new CacheEntry { Name = kvp.Value, Expiry = expiry };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Failed to look up display names for {Count} user IDs", uncached.Count);
                throw;
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.LogWarning(ex, "Malformed display-name response for {Count} user IDs", uncached.Count);
                throw;
            }
        }

        var nowAfter = DateTimeOffset.UtcNow;
        return requested.ToDictionary(
            id => id,
            id => TryGetFresh(id, nowAfter, out var name) ? name : id);
    }

    public async Task<string> GetDisplayNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return string.Empty;

        var names = await GetDisplayNamesAsync([userId], cancellationToken);
        return names.GetValueOrDefault(userId, userId);
    }

    private sealed class CacheEntry
    {
        public string Name { get; set; }
        public DateTimeOffset Expiry { get; set; }
    }
}
