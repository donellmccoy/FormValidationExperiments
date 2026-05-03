#nullable enable annotations
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Extensions;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// Base class for OData controllers providing shared infrastructure:
/// scoped <see cref="EctDbContext"/> creation, logging, and context factory injection.
/// </summary>
public abstract class ODataControllerBase : ODataController
{
    protected ILoggingService LoggingService { get; }

    protected IDbContextFactory<EctDbContext> ContextFactory { get; }

    protected TimeProvider TimeProvider { get; }

    protected ODataControllerBase(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService, TimeProvider timeProvider)
    {
        ContextFactory = contextFactory;
        LoggingService = loggingService;
        TimeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the authenticated user's unique identifier from the JWT <c>NameIdentifier</c> claim.
    /// Throws <see cref="UnauthorizedAccessException"/> when the claim is missing.
    /// </summary>
    protected string GetAuthenticatedUserId() => User.GetRequiredUserId();

    /// <summary>
    /// Creates a scoped <see cref="EctDbContext"/> and registers it for disposal at the end of the HTTP response.
    /// Use this helper when returning an <see cref="IQueryable"/> so the context remains alive during serialization.
    /// </summary>
    protected async Task<EctDbContext> CreateContextAsync(CancellationToken ct = default)
    {
        var context = await ContextFactory.CreateDbContextAsync(ct);

        HttpContext.Response.RegisterForDispose(context);

        return context;
    }

    /// <summary>
    /// Parses the <c>If-Match</c> request header into a <c>byte[]</c> RowVersion suitable for
    /// optimistic concurrency checks. Returns <c>true</c> when a valid ETag was supplied;
    /// otherwise returns <c>false</c> and assigns <paramref name="error"/> to a 428 (missing)
    /// or 400 (malformed) ProblemDetails result that the caller should return.
    /// </summary>
    protected bool TryGetIfMatchRowVersion(out byte[] rowVersion, out IActionResult? error)
    {
        rowVersion = [];
        error = null;

        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            error = Problem(
                title: "Precondition required",
                detail: "An If-Match header with the current ETag is required.",
                statusCode: StatusCodes.Status428PreconditionRequired);
            return false;
        }

        try
        {
            // Strip optional weak-validator prefix and surrounding quotes: W/"base64..." -> base64...
            var raw = ifMatch.Trim();
            if (raw.StartsWith("W/", StringComparison.Ordinal))
            {
                raw = raw[2..];
            }
            raw = raw.Trim('"');

            rowVersion = Convert.FromBase64String(raw);
            return true;
        }
        catch (FormatException)
        {
            error = Problem(
                title: "Bad request",
                detail: "The If-Match header contains an invalid ETag value.",
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }
    }
}
