using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECTSystem.Web.Models;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

public abstract class ODataServiceBase
{
    protected readonly EctODataContext Context;

    protected readonly HttpClient HttpClient;

    protected readonly ILogger Logger;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    protected ODataServiceBase(EctODataContext context, HttpClient httpClient, ILogger logger)
    {
        Context = context;
        HttpClient = httpClient;
        Logger = logger;
    }

    protected async Task<(List<T> Items, int Count)> ExecutePagedQueryAsync<T>(DataServiceQuery<T> query, CancellationToken ct = default)
    {
        var uri = query.IncludeCount().RequestUri;
        var response = await HttpClient.GetFromJsonAsync<ODataCountResponse<T>>(uri, JsonOptions, ct);

        return (response?.Value ?? [], response?.Count ?? 0);
    }

    protected async Task<List<T>> ExecuteQueryAsync<T>(DataServiceQuery<T> query, CancellationToken ct = default)
    {
        var uri = query.RequestUri;
        var response = await HttpClient.GetFromJsonAsync<ODataResponse<T>>(uri, JsonOptions, ct);

        return response?.Value ?? [];
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

    /// <summary>
    /// Attempts to parse the response body as an RFC 7807 <see cref="ApiProblemDetails"/>
    /// payload. Returns <c>null</c> if the body is empty, not JSON, or does not match
    /// the ProblemDetails shape. Never throws.
    /// </summary>
    protected async Task<ApiProblemDetails?> TryReadProblemDetailsAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.Content is null)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var looksLikeJson = mediaType is "application/problem+json" or "application/json"
            || (mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (!looksLikeJson)
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ApiProblemDetails>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogDebug(ex, "Failed to parse ProblemDetails body for {Operation}", response.RequestMessage?.RequestUri);
            return null;
        }
    }

    /// <summary>
    /// Throws an <see cref="EctApiException"/> carrying parsed ProblemDetails (when available)
    /// if <paramref name="response"/> indicates failure. The exception is logged with the
    /// supplied <paramref name="operation"/> label before being thrown.
    /// </summary>
    protected async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string operation, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await TryReadProblemDetailsAsync(response, ct);
        var message = problem?.Detail
            ?? problem?.Title
            ?? $"{operation} failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

        Logger.LogWarning(
            "API call {Operation} failed: status={StatusCode} title={ProblemTitle} detail={ProblemDetail}",
            operation,
            (int)response.StatusCode,
            problem?.Title,
            problem?.Detail);

        throw new EctApiException(operation, response.StatusCode, problem, message);
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
