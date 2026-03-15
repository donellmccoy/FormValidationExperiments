using ECTSystem.Shared.Models;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for member search operations.
/// Provides full-text search across <see cref="Member"/> entities with fuzzy matching
/// for military ranks (e.g., "Captain" maps to pay grades "O-3"/"O-6") and
/// service component enums (e.g., "Reserve" matches <c>ServiceComponent.AFR</c>).
/// Maps to <c>MembersController</c>.
/// </summary>
public interface IMemberService
{
    /// <summary>
    /// Searches for members by text across name, rank, unit, and service number fields.
    /// Includes fuzzy rank-to-pay-grade matching (e.g., "SSgt" also matches "E-5") and
    /// service component enum matching (e.g., "Guard" also matches <c>ANG</c>).
    /// Returns up to 50 matching members sorted by last name.
    /// </summary>
    /// <param name="searchText">The search text to match against member fields. Matching is case-insensitive and uses OData <c>contains()</c> filters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of <see cref="Member"/> entities matching the search text, or an empty list if no matches are found.</returns>
    Task<List<Member>> SearchMembersAsync(string searchText, CancellationToken cancellationToken = default);
}
