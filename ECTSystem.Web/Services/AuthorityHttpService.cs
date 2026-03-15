using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for LOD authority (reviewing official) operations.
/// Maps to the Authorities OData entity set.
/// </summary>
public class AuthorityHttpService : ODataServiceBase, IAuthorityService
{
    public AuthorityHttpService(ODataClient client, HttpClient httpClient)
        : base(client, httpClient) { }

    /// <inheritdoc />
    public async Task<List<LineOfDutyAuthority>> SaveAuthoritiesAsync(int caseId, ICollection<LineOfDutyAuthority> authorities, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentNullException.ThrowIfNull(authorities);

        // Step 1: Get existing authorities for this case.
        var query = Client.For<LineOfDutyAuthority>("Authorities")
            .Filter($"LineOfDutyCaseId eq {caseId}");

        var existingResponse = await Client.GetAsync(query, cancellationToken);

        var existingAuthorities = existingResponse.Value?.ToList() ?? [];
        var incomingRoles = authorities.Select(a => a.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Step 2: Delete authorities whose role is not in the incoming list.
        foreach (var toRemove in existingAuthorities.Where(a => !incomingRoles.Contains(a.Role)))
        {
            await Client.DeleteAsync("Authorities", toRemove.Id, null, cancellationToken);
        }

        // Step 3: Upsert — PATCH existing or POST new.
        var savedAuthorities = new List<LineOfDutyAuthority>();

        foreach (var incoming in authorities)
        {
            var match = existingAuthorities.FirstOrDefault(
                a => string.Equals(a.Role, incoming.Role, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                // PATCH existing authority.
                var patchBody = new Dictionary<string, object?>
                {
                    ["Name"] = incoming.Name,
                    ["Rank"] = incoming.Rank,
                    ["Title"] = incoming.Title,
                    ["ActionDate"] = incoming.ActionDate,
                    ["Recommendation"] = incoming.Recommendation,
                    ["Comments"] = incoming.Comments
                };

                var patched = await Client.UpdateAsync<LineOfDutyAuthority>(
                    "Authorities", match.Id, patchBody, null, cancellationToken);

                if (patched is not null) savedAuthorities.Add(patched);
            }
            else
            {
                // POST new authority.
                incoming.LineOfDutyCaseId = caseId;
                incoming.Id = 0;

                var created = await Client.CreateAsync(
                    "Authorities", incoming, null, cancellationToken);

                if (created is not null) savedAuthorities.Add(created);
            }
        }

        return savedAuthorities;
    }
}
