using ECTSystem.Shared.Models;
using Microsoft.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

public class AuthorityHttpService : ODataServiceBase, IAuthorityService
{
    public AuthorityHttpService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

    public async Task<List<LineOfDutyAuthority>> SaveAuthoritiesAsync(int caseId, ICollection<LineOfDutyAuthority> authorities, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentNullException.ThrowIfNull(authorities);

        // Step 1: Get existing authorities for this case.
        var query = Context.Authorities
            .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}");

        var existingAuthorities = await ExecuteQueryAsync(query, cancellationToken);
        var incomingRoles = authorities.Select(a => a.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Step 2: Queue deletes for authorities whose role is not in the incoming list.
        foreach (var toRemove in existingAuthorities.Where(a => !incomingRoles.Contains(a.Role)))
        {
            if (Context.GetEntityDescriptor(toRemove) == null)
            {
                Context.AttachTo("Authorities", toRemove);
            }
            Context.DeleteObject(toRemove);
        }

        // Step 3: Queue upserts — update existing or add new.
        var trackedEntities = new List<LineOfDutyAuthority>();

        foreach (var incoming in authorities)
        {
            var match = existingAuthorities.FirstOrDefault(
                a => string.Equals(a.Role, incoming.Role, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                match.Name = incoming.Name;
                match.Rank = incoming.Rank;
                match.Title = incoming.Title;
                match.ActionDate = incoming.ActionDate;
                match.Recommendation = incoming.Recommendation;
                match.Comments = incoming.Comments;

                if (Context.GetEntityDescriptor(match) == null)
                {
                    Context.AttachTo("Authorities", match);
                }

                Context.UpdateObject(match);
                trackedEntities.Add(match);
            }
            else
            {
                incoming.LineOfDutyCaseId = caseId;
                incoming.Id = 0;

                Context.AddObject("Authorities", incoming);
                trackedEntities.Add(incoming);
            }
        }

        // Step 4: Send all changes in a single $batch request.
        await Context.SaveChangesAsync(
            SaveChangesOptions.BatchWithSingleChangeset | SaveChangesOptions.UseJsonBatch,
            cancellationToken);

        // Detach all tracked entities.
        foreach (var entity in trackedEntities)
            Context.Detach(entity);

        foreach (var toRemove in existingAuthorities.Where(a => !incomingRoles.Contains(a.Role)))
        {
            if (Context.Entities.Any(e => e.Entity == toRemove))
                Context.Detach(toRemove);
        }

        return trackedEntities;
    }
}
