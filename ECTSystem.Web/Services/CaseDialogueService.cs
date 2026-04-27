using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.Extensions.Logging;

#nullable enable

namespace ECTSystem.Web.Services;

public class CaseDialogueService : ODataServiceBase, ICaseDialogueService
{
    public CaseDialogueService(EctODataContext context, HttpClient httpClient, ILogger<CaseDialogueService> logger)
        : base(context, httpClient, logger) { }

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

    public async Task AcknowledgeAsync(int commentId, string acknowledgedBy, CancellationToken ct = default)
    {
        var patch = new
        {
            IsAcknowledged = true,
            AcknowledgedDate = DateTime.UtcNow,
            AcknowledgedBy = acknowledgedBy
        };

        var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"odata/CaseDialogueComments({commentId})")
        {
            Content = JsonContent.Create(patch, options: JsonOptions)
        }, ct);

        await EnsureSuccessOrThrowAsync(response, $"PATCH odata/CaseDialogueComments({commentId})", ct);
    }
}
