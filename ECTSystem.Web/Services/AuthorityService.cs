using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

public class AuthorityService : ODataServiceBase, IAuthorityService
{
    public AuthorityService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

    public async Task<List<LineOfDutyAuthority>> SaveAuthoritiesAsync(int caseId, ICollection<LineOfDutyAuthority> authorities, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentNullException.ThrowIfNull(authorities);

        // Step 1: Get existing authorities for this case.
        var query = Context.Authorities
            .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}");

        var existingAuthorities = await ExecuteQueryAsync(query, cancellationToken);

        // Detach all queried entities so OData context doesn't interfere with HttpClient calls.
        foreach (var entity in existingAuthorities)
        {
            if (Context.GetEntityDescriptor(entity) != null)
            {
                Context.Detach(entity);
            }
        }

        var incomingRoles = authorities.Select(a => a.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var savedAuthorities = new List<LineOfDutyAuthority>();

        // Step 2: Delete authorities whose role is not in the incoming list.
        foreach (var toRemove in existingAuthorities.Where(a => !incomingRoles.Contains(a.Role)))
        {
            var response = await HttpClient.DeleteAsync($"odata/Authorities({toRemove.Id})", cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        // Step 3: Upsert — update existing or add new.
        foreach (var incoming in authorities)
        {
            var match = existingAuthorities.FirstOrDefault(
                a => string.Equals(a.Role, incoming.Role, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                var patchBody = new
                {
                    incoming.Name,
                    incoming.Rank,
                    incoming.Title,
                    incoming.ActionDate,
                    incoming.Recommendation,
                    incoming.Comments
                };

                var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"odata/Authorities({match.Id})")
                {
                    Content = JsonContent.Create(patchBody, options: JsonOptions)
                };

                var response = await HttpClient.SendAsync(patchRequest, cancellationToken);
                response.EnsureSuccessStatusCode();

                var updated = await response.Content.ReadFromJsonAsync<LineOfDutyAuthority>(JsonOptions, cancellationToken);
                savedAuthorities.Add(updated!);
            }
            else
            {
                var dto = new CreateAuthorityDto
                {
                    LineOfDutyCaseId = caseId,
                    Role = incoming.Role,
                    Name = incoming.Name,
                    Rank = incoming.Rank,
                    Title = incoming.Title,
                    ActionDate = incoming.ActionDate,
                    Recommendation = incoming.Recommendation,
                    Comments = incoming.Comments
                };

                var response = await HttpClient.PostAsJsonAsync("odata/Authorities", dto, JsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();

                var created = await response.Content.ReadFromJsonAsync<LineOfDutyAuthority>(JsonOptions, cancellationToken);
                savedAuthorities.Add(created!);
            }
        }

        return savedAuthorities;
    }
}
