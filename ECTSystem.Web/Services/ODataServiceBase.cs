using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

public abstract class ODataServiceBase
{
    protected readonly EctODataContext Context;

    protected readonly HttpClient HttpClient;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    protected ODataServiceBase(EctODataContext context, HttpClient httpClient)
    {
        Context = context;
        HttpClient = httpClient;
    }

    protected static async Task<(List<T> Items, int Count)> ExecutePagedQueryAsync<T>(DataServiceQuery<T> query, CancellationToken ct = default)
    {
        var response = await query.IncludeCount().ExecuteAsync(ct)
            as QueryOperationResponse<T>;

        var items = response?.ToList() ?? [];
        var count = (int)(response?.Count ?? 0);

        return (items, count);
    }

    protected static async Task<List<T>> ExecuteQueryAsync<T>(DataServiceQuery<T> query, CancellationToken ct = default)
    {
        var response = await query.ExecuteAsync(ct);

        return [.. response];
    }

    protected static string BuildNavigationPropertyUrl(string basePath, string? filter, int? top, int? skip, string? orderby, bool? count, string? select = null, string? expand = null)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(expand))
        {
            parts.Add($"$expand={Uri.EscapeDataString(expand)}");
        }

        if (!string.IsNullOrEmpty(select))
        {
            parts.Add($"$select={Uri.EscapeDataString(select)}");
        }

        if (!string.IsNullOrEmpty(filter))
        {
            parts.Add($"$filter={Uri.EscapeDataString(filter)}");
        }

        if (top.HasValue)
        {
            parts.Add($"$top={top.Value}");
        }

        if (skip.HasValue)
        {
            parts.Add($"$skip={skip.Value}");
        }

        if (!string.IsNullOrEmpty(orderby))
        {
            parts.Add($"$orderby={Uri.EscapeDataString(orderby)}");
        }

        if (count == true)
        {
            parts.Add("$count=true");
        }

        return parts.Count > 0 ? $"{basePath}?{string.Join("&", parts)}" : basePath;
    }

    protected class ODataCountResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];

        [JsonPropertyName("@odata.count")]
        public int Count { get; set; }
    }

    protected class ODataResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];
    }
}
