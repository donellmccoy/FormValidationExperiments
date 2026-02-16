using Microsoft.AspNetCore.OData.Deltas;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service interface for Line of Duty case CRUD operations.
/// </summary>
public interface IDataService
{
    IQueryable<LineOfDutyCase> GetCasesQueryable();
    Task<LineOfDutyCase> GetCaseByKeyAsync(int key, CancellationToken ct = default);
    Task<LineOfDutyCase> CreateCaseAsync(LineOfDutyCase lodCase, CancellationToken ct = default);
    Task<LineOfDutyCase> UpdateCaseAsync(int key, LineOfDutyCase update, CancellationToken ct = default);

    /// <summary>
    /// Applies a partial update using OData Delta semantics — only the properties
    /// present in the Delta are written to the entity via <see cref="Delta{T}.Patch"/>.
    /// </summary>
    Task<LineOfDutyCase> PatchCaseAsync(int key, Delta<LineOfDutyCase> delta, CancellationToken ct = default);

    Task<bool> DeleteCaseAsync(int key, CancellationToken ct = default);
}
