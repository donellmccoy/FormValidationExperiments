using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Persistence.Models;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Api.Controllers;

[Authorize]
public class CaseDialogueCommentsController : ODataControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CaseDialogueCommentsController(
        IDbContextFactory<EctDbContext> contextFactory,
        ILoggingService loggingService,
        TimeProvider timeProvider,
        UserManager<ApplicationUser> userManager)
        : base(contextFactory, loggingService, timeProvider)
    {
        _userManager = userManager;
    }

    private async Task<string> ResolveAuthorNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }
            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                return user.UserName;
            }
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }
        }

        return User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? userId;
    }

    /// <summary>
    /// Returns all case dialogue comments as an IQueryable for OData query composition.
    /// OData route: GET /odata/CaseDialogueComments
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 2, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var context = await CreateContextAsync(ct);
        return Ok(context.CaseDialogueComments.AsNoTracking());
    }

    /// <summary>
    /// Creates a new dialogue comment for the specified case.
    /// OData route: POST /odata/CaseDialogueComments
    /// </summary>
    /// <param name="dto">The comment creation DTO.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Post([FromBody] CreateCaseDialogueCommentDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var caseExists = await context.Cases.AnyAsync(c => c.Id == dto.LineOfDutyCaseId, ct);
        if (!caseExists)
        {
            return Problem(
                title: "Not found",
                detail: $"No case exists with ID {dto.LineOfDutyCaseId}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var comment = CaseDialogueCommentDtoMapper.ToEntity(dto);
        var userId = GetAuthenticatedUserId();
        comment.AuthorName = await ResolveAuthorNameAsync(userId);
        if (string.IsNullOrWhiteSpace(comment.AuthorRole))
        {
            comment.AuthorRole = User.FindFirstValue(ClaimTypes.Role);
        }

        context.CaseDialogueComments.Add(comment);
        await context.SaveChangesAsync(ct);
        return Created(comment);
    }

    /// <summary>
    /// Partially updates an existing dialogue comment.
    /// OData route: PATCH /odata/CaseDialogueComments({key})
    /// </summary>
    /// <param name="key">The comment identifier.</param>
    /// <param name="delta">The partial update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<CaseDialogueComment> delta, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var comment = await context.CaseDialogueComments.FindAsync([key], ct);

        if (comment is null)
        {
            return Problem(
                title: "Not found",
                detail: $"No dialogue comment exists with ID {key}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var originalRowVersion = comment.RowVersion;
        delta.Patch(comment);

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(comment).Property(e => e.RowVersion).OriginalValue = originalRowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                title: "Concurrency conflict",
                detail: "The comment was modified by another user. Refresh and retry.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Updated(comment);
    }

    /// <summary>
    /// Deletes a dialogue comment. Only the author, Admin, or CaseManager may delete.
    /// OData route: DELETE /odata/CaseDialogueComments({key})
    /// </summary>
    /// <param name="key">The comment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var comment = await context.CaseDialogueComments.FindAsync([key], ct);

        if (comment is null)
        {
            return Problem(
                title: "Not found",
                detail: $"No dialogue comment exists with ID {key}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var userId = GetAuthenticatedUserId();
        if (comment.CreatedBy != userId && !User.IsInRole("Admin") && !User.IsInRole("CaseManager"))
        {
            return Problem(
                title: "Forbidden",
                detail: "You can only delete your own comments.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        context.CaseDialogueComments.Remove(comment);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }
}
