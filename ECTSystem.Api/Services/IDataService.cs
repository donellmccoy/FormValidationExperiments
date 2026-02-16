using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

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
    /// Applies a partial update using only the changed property names from an OData Delta.
    /// </summary>
    Task<LineOfDutyCase> PatchCaseScalarsAsync(int key, LineOfDutyCasePatchDto dto, IEnumerable<string> changedProperties, CancellationToken ct = default);

    /// <summary>
    /// Replaces the Authorities collection for a case with the incoming set
    /// (add/update/remove semantics).
    /// </summary>
    Task<List<LineOfDutyAuthority>> SyncAuthoritiesAsync(int key, List<LineOfDutyAuthority> incoming, CancellationToken ct = default);

    Task<bool> DeleteCaseAsync(int key, CancellationToken ct = default);
}
