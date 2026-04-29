using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.Extensions.Logging;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side OData service for reading <see cref="CaseDialogueComment"/> threads,
/// posting new comments, and acknowledging existing ones.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AcknowledgeAsync"/> targets the server-side bound action
/// <c>Default.Acknowledge</c> so the acknowledgment timestamp is stamped by the server's
/// <c>TimeProvider</c> rather than the client clock. Do not reintroduce a client-side
/// <c>DateTime.UtcNow</c> in this path.
/// </para>
/// <para>
/// <see cref="GetCommentsAsync"/> uses <see cref="ODataServiceBase.BuildNavigationPropertyUrl"/>
/// to compose a paged, ordered <c>$filter</c> + <c>$count=true</c> request. The default page
/// size is 20 and the order is newest-first (<c>CreatedDate desc</c>); callers that need a
/// different ordering should request it explicitly rather than reordering client-side.
/// </para>
/// </remarks>
public class CaseDialogueService : ODataServiceBase, ICaseDialogueService
{
    public CaseDialogueService(
        EctODataContext context,
        HttpClient httpClient,
        ILogger<CaseDialogueService> logger,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
        : base(context, httpClient, logger, jsonOptions) { }

    public async Task<PagedResult<CaseDialogueComment>> GetCommentsAsync(int caseId, int top = 20, int skip = 0, CancellationToken ct = default)
    {
        var url = BuildNavigationPropertyUrl(
            "odata/CaseDialogueComments",
            filter: $"LineOfDutyCaseId eq {caseId}",
            top: top,
            skip: skip,
            orderby: "CreatedDate desc",
            count: true);

        var response = await HttpClient.GetFromJsonAsync<ODataCountResponse<CaseDialogueComment>>(url, JsonOptions, ct);

        return new PagedResult<CaseDialogueComment>
        {
            Items = response?.Value ?? [],
            TotalCount = response?.Count ?? 0
        };
    }

    public async Task<CaseDialogueComment> PostCommentAsync(CaseDialogueComment comment, CancellationToken ct = default)
    {
        var response = await HttpClient.PostAsJsonAsync("odata/CaseDialogueComments", comment, JsonOptions, ct);
        await EnsureSuccessOrThrowAsync(response, "POST odata/CaseDialogueComments", ct);
        return (await response.Content.ReadFromJsonAsync<CaseDialogueComment>(JsonOptions, ct))!;
    }

    public async Task AcknowledgeAsync(int commentId, CancellationToken ct = default)
    {
        var response = await HttpClient.PostAsync(
            $"odata/CaseDialogueComments({commentId})/Default.Acknowledge",
            content: null,
            ct);

        await EnsureSuccessOrThrowAsync(response, $"POST odata/CaseDialogueComments({commentId})/Default.Acknowledge", ct);
    }
}
