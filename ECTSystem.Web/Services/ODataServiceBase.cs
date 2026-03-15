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
    /// <summary>
    /// The OData client used for strongly-typed CRUD operations against OData entity sets.
    /// Handles query building, serialization, and response parsing for standard OData requests.
    /// </summary>
    protected readonly ODataClient Client;

    /// <summary>
    /// The raw <see cref="System.Net.Http.HttpClient"/> used for custom HTTP requests
    /// that fall outside standard OData CRUD patterns (e.g., navigation property queries,
    /// multipart uploads, or custom action/function endpoints).
    /// </summary>
    protected readonly HttpClient HttpClient;

    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> configured for OData JSON payloads.
    /// Uses case-insensitive property matching, preserves original property casing,
    /// and serializes/deserializes enums as their string names.
    /// </summary>
    protected static readonly JsonSerializerOptions ODataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataServiceBase"/> class.
    /// </summary>
    /// <param name="client">The OData client for strongly-typed entity operations.</param>
    /// <param name="httpClient">The raw HTTP client for custom requests outside standard OData patterns.</param>
    protected ODataServiceBase(ODataClient client, HttpClient httpClient)
    {
        Client = client;
        HttpClient = httpClient;
    }

    /// <summary>
    /// Builds a fully-qualified OData URL from a base path and optional query parameters.
    /// Appends standard OData system query options (<c>$select</c>, <c>$filter</c>, <c>$top</c>,
    /// <c>$skip</c>, <c>$orderby</c>, <c>$count</c>) as query-string segments.
    /// </summary>
    /// <param name="basePath">The base OData URL path (e.g., <c>odata/Cases(1)/Documents</c>).</param>
    /// <param name="filter">An OData <c>$filter</c> expression, or <c>null</c> to omit.</param>
    /// <param name="top">The maximum number of records to return (<c>$top</c>), or <c>null</c> to omit.</param>
    /// <param name="skip">The number of records to skip for paging (<c>$skip</c>), or <c>null</c> to omit.</param>
    /// <param name="orderby">An OData <c>$orderby</c> expression, or <c>null</c> to omit.</param>
    /// <param name="count">If <c>true</c>, includes <c>$count=true</c> to request an inline count in the response.</param>
    /// <param name="select">An OData <c>$select</c> expression to limit returned properties, or <c>null</c> to omit.</param>
    /// <returns>The assembled URL string with query parameters appended, or the bare <paramref name="basePath"/> if no parameters are specified.</returns>
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

    /// <summary>
    /// Generic OData response DTO for queries that include an inline <c>@odata.count</c>.
    /// Used when deserializing raw <see cref="System.Net.Http.HttpClient"/> responses for
    /// navigation property endpoints that return paged collections with a total count.
    /// </summary>
    /// <typeparam name="T">The entity type contained in the response collection.</typeparam>
    protected class ODataCountResponse<T>
    {
        /// <summary>
        /// Gets or sets the collection of entities returned by the OData query.
        /// Maps to the <c>value</c> JSON property in the OData response payload.
        /// </summary>
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];

        /// <summary>
        /// Gets or sets the total count of matching entities on the server, regardless of paging.
        /// Maps to the <c>@odata.count</c> JSON property in the OData response payload.
        /// Only populated when <c>$count=true</c> was included in the request query string.
        /// </summary>
        [JsonPropertyName("@odata.count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Generic OData response DTO for queries that return a collection without an inline count.
    /// Used when deserializing raw <see cref="System.Net.Http.HttpClient"/> responses for simple
    /// navigation property endpoints that do not request <c>$count</c>.
    /// </summary>
    /// <typeparam name="T">The entity type contained in the response collection.</typeparam>
    protected class ODataResponse<T>
    {
        /// <summary>
        /// Gets or sets the collection of entities returned by the OData query.
        /// Maps to the <c>value</c> JSON property in the OData response payload.
        /// </summary>
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];
    }
}
