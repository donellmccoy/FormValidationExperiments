using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using Microsoft.Extensions.Logging;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side OData service for searching <see cref="Member"/> records.
/// </summary>
/// <remarks>
/// <para>
/// Search is delegated to the server-side OData action
/// <c>POST /odata/Members/Search</c>. The server performs free-text matching across
/// <c>FirstName</c>, <c>LastName</c>, <c>Rank</c>, <c>Unit</c>, and <c>ServiceNumber</c>,
/// expands rank-name aliases (e.g. "Sergeant") into pay-grade equality matches, and matches
/// <see cref="ECTSystem.Shared.Enums.ServiceComponent"/> values by raw name or display name
/// ("Reserve" → AirForceReserve). Results are bounded to 25 ordered by LastName, FirstName.
/// </para>
/// <para>
/// User input flows over the wire as a JSON-bodied action parameter — no client-side OData
/// <c>$filter</c> string composition or single-quote escaping is performed here, eliminating
/// the prior injection-surface concern.
/// </para>
/// </remarks>
public class MemberService : ODataServiceBase, IMemberService
{
    public MemberService(
        EctODataContext context,
        HttpClient httpClient,
        ILogger<MemberService> logger,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
        : base(context, httpClient, logger, jsonOptions) { }

    /// <summary>
    /// Searches members across name, rank, unit, service number, pay grade (via rank lookup),
    /// and service component, returning at most 25 matches ordered by last name then first name.
    /// </summary>
    /// <remarks>
    /// Invokes the server-side bound OData action <c>Members/Search</c>. The server is the
    /// sole authority for the matching strategy; the client only forwards the raw search text.
    /// </remarks>
    public async Task<List<Member>> SearchMembersAsync(string searchText, CancellationToken cancellationToken = default)
    {
        var body = new { searchText = searchText ?? string.Empty };

        var response = await HttpClient.PostAsJsonAsync("odata/Members/Search", body, JsonOptions, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "POST odata/Members/Search", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<ODataResponse<Member>>(JsonOptions, cancellationToken);

        return result?.Value ?? [];
    }
}
