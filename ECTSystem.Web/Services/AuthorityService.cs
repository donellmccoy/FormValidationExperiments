using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side OData service for <see cref="LineOfDutyAuthority"/> records attached to a case.
/// Authorities are role-keyed (one row per <c>Role</c> per case), so save semantics are
/// upsert-by-role rather than primary-key replace.
/// </summary>
/// <remarks>
/// <para>
/// Until a server-side bound action exists, <see cref="SaveAuthoritiesAsync"/> performs the
/// reconcile in three sequential HTTP calls per delta (1 GET + N writes). Callers should
/// treat the operation as <b>not transactional</b> across the wire — a network or 5xx mid-loop
/// can leave the case in a partially-saved state. UI flows therefore reload the case after
/// save so the user sees authoritative server state, not the optimistic local list.
/// </para>
/// <para>
/// Role comparisons are <see cref="StringComparer.OrdinalIgnoreCase"/> on both the existing-set
/// hash lookup and the upsert match — keep both sides aligned if the comparison changes.
/// </para>
/// </remarks>
public class AuthorityService : ODataServiceBase, IAuthorityService
{
    public AuthorityService(
        EctODataContext context,
        HttpClient httpClient,
        ILogger<AuthorityService> logger,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
        : base(context, httpClient, logger, jsonOptions) { }

    /// <summary>
    /// Reconciles the authorities for <paramref name="caseId"/> against <paramref name="authorities"/>:
    /// deletes rows whose <c>Role</c> is no longer present, PATCHes rows whose role matches an
    /// existing entry, and POSTs rows for new roles. Returns the saved (server-echoed) authorities.
    /// </summary>
    /// <remarks>
    /// Not atomic — see the class-level remarks. Each per-row failure throws via
    /// <c>EnsureSuccessOrThrowAsync</c> and aborts the loop; rows processed before the failure
    /// remain persisted on the server. Detaches each queried entity from the OData context so the
    /// subsequent raw <see cref="HttpClient"/> writes are not interfered with by tracked-entity state.
    /// </remarks>
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
            await EnsureSuccessOrThrowAsync(response, $"DELETE odata/Authorities({toRemove.Id})", cancellationToken);
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
                await EnsureSuccessOrThrowAsync(response, $"PATCH odata/Authorities({match.Id})", cancellationToken);

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
                await EnsureSuccessOrThrowAsync(response, "POST odata/Authorities", cancellationToken);

                var created = await response.Content.ReadFromJsonAsync<LineOfDutyAuthority>(JsonOptions, cancellationToken);
                savedAuthorities.Add(created!);
            }
        }

        return savedAuthorities;
    }
}
