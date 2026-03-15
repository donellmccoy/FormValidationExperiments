using ECTSystem.Shared.Models;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for LOD authority (reviewing official) operations.
/// Maps to the Authorities OData entity set.
/// </summary>
public interface IAuthorityService
{
    /// <summary>
    /// Saves (upserts and prunes) the reviewing authorities for a LOD case.
    /// Matches by Role — patches existing, creates new, and deletes removed.
    /// </summary>
    Task<List<LineOfDutyAuthority>> SaveAuthoritiesAsync(int caseId, ICollection<LineOfDutyAuthority> authorities, CancellationToken cancellationToken = default);
}
