using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

[Authorize]
public class CaseDialogueCommentsController : ODataControllerBase
{
    public CaseDialogueCommentsController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 2, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var context = await CreateContextAsync(ct);
        return Ok(context.CaseDialogueComments.AsNoTracking());
    }

    [EnableQuery]
    public async Task<IActionResult> Post([FromBody] CaseDialogueComment comment, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var caseExists = await context.Cases.AnyAsync(c => c.Id == comment.LineOfDutyCaseId, ct);
        if (!caseExists)
        {
            return Problem(
                title: "Not found",
                detail: $"No case exists with ID {comment.LineOfDutyCaseId}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        context.CaseDialogueComments.Add(comment);
        await context.SaveChangesAsync(ct);
        return Created(comment);
    }

    public async Task<IActionResult> Patch([FromODataUri] int key, [FromBody] Delta<CaseDialogueComment> delta, CancellationToken ct = default)
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

        delta.Patch(comment);
        await context.SaveChangesAsync(ct);
        return Updated(comment);
    }

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

        context.CaseDialogueComments.Remove(comment);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }
}
