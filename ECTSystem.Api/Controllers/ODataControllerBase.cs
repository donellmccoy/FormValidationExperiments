using System.Security.Claims;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// Base class for OData controllers providing shared infrastructure:
/// scoped <see cref="EctDbContext"/> creation, logging, and context factory injection.
/// </summary>
public abstract class ODataControllerBase : ODataController
{
    protected readonly ILoggingService LoggingService;
    protected readonly IDbContextFactory<EctDbContext> ContextFactory;

    protected ODataControllerBase(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
    {
        ContextFactory = contextFactory;
        LoggingService = loggingService;
    }

    /// <summary>
    /// Returns the authenticated user's unique identifier from the JWT <c>NameIdentifier</c> claim.
    /// Throws <see cref="UnauthorizedAccessException"/> when the claim is missing.
    /// </summary>
    protected string GetAuthenticatedUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing NameIdentifier claim.");

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
}
