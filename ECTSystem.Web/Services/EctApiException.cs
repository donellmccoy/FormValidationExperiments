using System.Net;
using ECTSystem.Web.Models;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Dedicated exception type thrown by client services when an outbound HTTP call
/// fails. Carries the HTTP status code and, when available, the parsed RFC 7807
/// <see cref="ApiProblemDetails"/> body returned by the server so the UI can
/// surface a meaningful error message instead of swallowing the failure.
/// </summary>
public sealed class EctApiException : Exception
{
    /// <summary>
    /// HTTP status code returned by the failed call.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Parsed ProblemDetails payload, when the server returned one in the response body.
    /// </summary>
    public ApiProblemDetails? ProblemDetails { get; }

    /// <summary>
    /// Short label identifying which client operation failed (e.g., <c>"POST odata/Cases"</c>).
    /// Useful for diagnostics and logging.
    /// </summary>
    public string Operation { get; }

    public EctApiException(string operation, HttpStatusCode statusCode, ApiProblemDetails? problemDetails, string message, Exception? inner = null)
        : base(message, inner)
    {
        Operation = operation;
        StatusCode = statusCode;
        ProblemDetails = problemDetails;
    }
}
