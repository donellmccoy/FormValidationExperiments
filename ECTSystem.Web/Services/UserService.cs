using System.Net.Http.Json;

namespace ECTSystem.Web.Services;

public class UserService(HttpClient httpClient) : IUserService
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
