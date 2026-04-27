using System.Text.Json.Serialization;

#nullable enable

namespace ECTSystem.Web.Models;

/// <summary>
/// Lightweight client-side representation of an RFC 7807 ProblemDetails payload
/// returned by the API. Used by <see cref="Services.ODataServiceBase"/> to surface
/// server-issued problem responses to client services and the UI.
/// </summary>
public sealed class ApiProblemDetails
{
    /// <summary>
    /// A URI reference identifying the problem type. Defaults to <c>about:blank</c> on the server.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Short, human-readable summary of the problem type.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// HTTP status code echoed in the body.
    /// </summary>
    [JsonPropertyName("status")]
    public int? Status { get; set; }

    /// <summary>
    /// Human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// URI reference identifying the specific occurrence of the problem.
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; set; }
}
