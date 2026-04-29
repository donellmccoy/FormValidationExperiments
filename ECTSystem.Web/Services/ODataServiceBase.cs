using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECTSystem.Web.Extensions;
using ECTSystem.Web.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Shared base for OData-backed client services. Owns the typed OData client
/// (<see cref="EctODataContext"/>), the underlying <see cref="HttpClient"/>, and shared
/// <see cref="JsonSerializerOptions"/>, plus helpers for paged queries and ProblemDetails-aware
/// error translation.
/// </summary>
/// <remarks>
/// <para><b>Convention — OData client vs. HttpClient:</b></para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b><see cref="Context"/> (typed OData client)</b> — preferred for reads/queries
///       expressible via <c>$filter</c>, <c>$top</c>, <c>$skip</c>, <c>$expand</c>,
///       <c>$count</c>, navigation collections, and tracked entity materialization.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b><see cref="HttpClient"/></b> — used for what the typed client cannot model
///       cleanly: bound actions, <c>$batch</c>, multipart uploads, and arbitrary <c>PATCH</c>
///       payloads. Failed responses must flow through <see cref="EnsureSuccessOrThrowAsync"/>
///       so the caller observes a single typed <see cref="EctApiException"/> with parsed
///       <see cref="ApiProblemDetails"/>.
///     </description>
///   </item>
/// </list>
/// <para>
/// New derived services should follow this split rather than mixing approaches in the same
/// operation. Cancellation tokens must be propagated on every async path.
/// </para>
/// </remarks>
public abstract class ODataServiceBase
{
    protected readonly EctODataContext Context;

    protected readonly HttpClient HttpClient;

    protected readonly ILogger Logger;

    /// <summary>
    /// OData-wire (PascalCase) JSON options resolved from the keyed DI singleton
    /// registered under <see cref="ServiceCollectionExtensions.ODataJsonOptionsKey"/>.
    /// </summary>
    protected JsonSerializerOptions JsonOptions { get; }

    protected ODataServiceBase(
        EctODataContext context,
        HttpClient httpClient,
        ILogger logger,
        [FromKeyedServices(ServiceCollectionExtensions.ODataJsonOptionsKey)] JsonSerializerOptions jsonOptions)
    {
        Context = context;
        HttpClient = httpClient;
        Logger = logger;
        JsonOptions = jsonOptions;
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

    /// <summary>
    /// Typed, homogeneous OData <c>$batch</c> helper using the JSON batch format
    /// (per OData v4 spec §11.7). Posts all <paramref name="bodies"/> as individual
    /// requests against the same <paramref name="entitySetPath"/> with the same
    /// <paramref name="method"/>, then deserializes each response body to <typeparamref name="TResponse"/>.
    /// </summary>
    /// <remarks>
    /// Replaces N+1 round-trips with a single HTTP request. The server is wired with
    /// <c>DefaultODataBatchHandler</c> in <c>ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs</c>.
    /// On any non-success sub-response (or transport error) the entire call surfaces a single
    /// <see cref="EctApiException"/> via <see cref="EnsureSuccessOrThrowAsync"/> with the
    /// first failed sub-response. Results are returned in request order.
    /// </remarks>
    protected async Task<List<TResponse>> BatchPostJsonAsync<TRequest, TResponse>(
        string entitySetPath,
        IReadOnlyList<TRequest> bodies,
        string operation,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitySetPath);
        ArgumentNullException.ThrowIfNull(bodies);

        if (bodies.Count == 0)
        {
            return [];
        }

        var requests = new List<BatchRequestItem>(bodies.Count);
        for (var i = 0; i < bodies.Count; i++)
        {
            requests.Add(new BatchRequestItem
            {
                Id = (i + 1).ToString(),
                Method = "POST",
                Url = $"/odata/{entitySetPath}",
                Headers = new Dictionary<string, string>
                {
                    ["content-type"] = "application/json",
                    ["accept"] = "application/json"
                },
                Body = JsonSerializer.SerializeToElement(bodies[i], JsonOptions)
            });
        }

        var batchPayload = new BatchEnvelope { Requests = requests };
        var response = await HttpClient.PostAsJsonAsync("odata/$batch", batchPayload, JsonOptions, ct);
        await EnsureSuccessOrThrowAsync(response, $"{operation} ($batch)", ct);

        var envelope = await response.Content.ReadFromJsonAsync<BatchResponseEnvelope>(JsonOptions, ct)
            ?? throw new EctApiException(operation, response.StatusCode, null, "OData $batch response was empty");

        var results = new List<TResponse>(envelope.Responses.Count);
        foreach (var sub in envelope.Responses.OrderBy(r => int.TryParse(r.Id, out var n) ? n : 0))
        {
            if (sub.Status is < 200 or >= 300)
            {
                var detail = sub.Body.ValueKind == JsonValueKind.Undefined
                    ? $"sub-request {sub.Id} returned {sub.Status}"
                    : sub.Body.ToString();
                Logger.LogWarning("OData $batch sub-request {SubId} for {Operation} failed: status={Status} body={Body}",
                    sub.Id, operation, sub.Status, detail);
                throw new EctApiException(operation, (System.Net.HttpStatusCode)sub.Status, null,
                    $"{operation}: $batch sub-request {sub.Id} failed with status {sub.Status}");
            }

            if (sub.Body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                results.Add(default!);
                continue;
            }

            var item = sub.Body.Deserialize<TResponse>(JsonOptions);
            results.Add(item!);
        }

        return results;
    }

    private sealed class BatchEnvelope
    {
        [JsonPropertyName("requests")]
        public List<BatchRequestItem> Requests { get; set; } = [];
    }

    private sealed class BatchRequestItem
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("method")] public string Method { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("headers")] public Dictionary<string, string> Headers { get; set; } = [];
        [JsonPropertyName("body")] public JsonElement Body { get; set; }
    }

    private sealed class BatchResponseEnvelope
    {
        [JsonPropertyName("responses")]
        public List<BatchResponseItem> Responses { get; set; } = [];
    }

    private sealed class BatchResponseItem
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("headers")] public Dictionary<string, string> Headers { get; set; } = [];
        [JsonPropertyName("body")] public JsonElement Body { get; set; }
    }
}
