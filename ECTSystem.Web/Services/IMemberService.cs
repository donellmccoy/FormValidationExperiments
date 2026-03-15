using ECTSystem.Shared.Models;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for member search operations.
/// Maps to <c>MembersController</c>.
/// </summary>
public interface IMemberService
{
    /// <summary>
    /// Searches for members by text across name, rank, unit, and service number.
    /// Includes fuzzy rank-to-pay-grade matching and service component enum matching.
    /// </summary>
    Task<List<Member>> SearchMembersAsync(string searchText, CancellationToken cancellationToken = default);
}
