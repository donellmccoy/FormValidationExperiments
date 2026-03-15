using ECTSystem.Shared.Models;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for LOD authority (reviewing official) operations.
/// Manages the lifecycle of <see cref="LineOfDutyAuthority"/> entries that represent
/// the officials who review and endorse each stage of a Line of Duty determination
/// (e.g., Unit Commander, SJA, Medical Provider). Maps to the Authorities OData entity set.
/// </summary>
public interface IAuthorityService
{
    /// <summary>
    /// Saves (upserts and prunes) the reviewing authorities for a LOD case.
    /// Matches existing authorities by <see cref="LineOfDutyAuthority.Role"/> — patches existing entries
    /// with updated data, creates new entries for roles not yet persisted, and deletes any
    /// server-side entries whose roles are no longer present in the incoming collection.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case whose authorities are being saved.</param>
    /// <param name="authorities">The complete set of authorities to persist. Entries are matched by <c>Role</c> for upsert logic.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of the saved <see cref="LineOfDutyAuthority"/> entries as returned by the server, including server-generated IDs and timestamps.</returns>
    Task<List<LineOfDutyAuthority>> SaveAuthoritiesAsync(int caseId, ICollection<LineOfDutyAuthority> authorities, CancellationToken cancellationToken = default);
}
