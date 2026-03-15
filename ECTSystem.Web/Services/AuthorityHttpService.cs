using System.Net.Http.Json;
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
        var existingResponse = await HttpClient.GetFromJsonAsync<ODataResponse<LineOfDutyAuthority>>(
            $"odata/Authorities?$filter=LineOfDutyCaseId eq {caseId}", ODataJsonOptions, cancellationToken);

        var existingAuthorities = existingResponse?.Value ?? [];
        var incomingRoles = authorities.Select(a => a.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Step 2: Delete authorities whose role is not in the incoming list.
        foreach (var toRemove in existingAuthorities.Where(a => !incomingRoles.Contains(a.Role)))
        {
            var deleteResponse = await HttpClient.DeleteAsync($"odata/Authorities({toRemove.Id})", cancellationToken);
            deleteResponse.EnsureSuccessStatusCode();
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
                var patchBody = new
                {
                    incoming.Name,
                    incoming.Rank,
                    incoming.Title,
                    incoming.ActionDate,
                    incoming.Recommendation,
                    incoming.Comments
                };
                var patchContent = JsonContent.Create(patchBody, options: ODataJsonOptions);
                var patchResponse = await HttpClient.PatchAsync($"odata/Authorities({match.Id})", patchContent, cancellationToken);
                patchResponse.EnsureSuccessStatusCode();
                var patched = await patchResponse.Content.ReadFromJsonAsync<LineOfDutyAuthority>(ODataJsonOptions, cancellationToken);
                if (patched is not null) savedAuthorities.Add(patched);
            }
            else
            {
                // POST new authority.
                incoming.LineOfDutyCaseId = caseId;
                incoming.Id = 0;
                var postResponse = await HttpClient.PostAsJsonAsync("odata/Authorities", incoming, ODataJsonOptions, cancellationToken);
                postResponse.EnsureSuccessStatusCode();
                var created = await postResponse.Content.ReadFromJsonAsync<LineOfDutyAuthority>(ODataJsonOptions, cancellationToken);
                if (created is not null) savedAuthorities.Add(created);
            }
        }

        return savedAuthorities;
    }
}
