using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PanoramicData.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Base class for OData HTTP services. Provides shared infrastructure
/// including the OData client, raw HttpClient, JSON serializer options,
/// URL-building helpers, and response deserialization DTOs.
/// </summary>
public abstract class ODataServiceBase
{
    protected readonly ODataClient Client;
    protected readonly HttpClient HttpClient;

    protected static readonly JsonSerializerOptions ODataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    protected ODataServiceBase(ODataClient client, HttpClient httpClient)
    {
        Client = client;
        HttpClient = httpClient;
    }

    protected static string BuildNavigationPropertyUrl(
        string basePath, string? filter, int? top, int? skip, string? orderby, bool? count, string? select = null)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(select)) parts.Add($"$select={select}");
        if (!string.IsNullOrEmpty(filter)) parts.Add($"$filter={filter}");
        if (top.HasValue) parts.Add($"$top={top.Value}");
        if (skip.HasValue) parts.Add($"$skip={skip.Value}");
        if (!string.IsNullOrEmpty(orderby)) parts.Add($"$orderby={orderby}");
        if (count == true) parts.Add("$count=true");

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
