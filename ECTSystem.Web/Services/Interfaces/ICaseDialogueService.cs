using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

#nullable enable

namespace ECTSystem.Web.Services;

public interface ICaseDialogueService
{
    Task<PagedResult<CaseDialogueComment>> GetCommentsAsync(int caseId, int top = 20, int skip = 0, CancellationToken ct = default);
    Task<CaseDialogueComment> PostCommentAsync(CaseDialogueComment comment, CancellationToken ct = default);
    Task AcknowledgeAsync(int commentId, string acknowledgedBy, CancellationToken ct = default);
}
