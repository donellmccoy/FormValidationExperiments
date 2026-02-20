using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

public interface ICaseBookmarkService
{
    IQueryable<CaseBookmark> GetBookmarksQueryable(string userId);
    Task<CaseBookmark> AddBookmarkAsync(string userId, int caseId, CancellationToken ct = default);
    Task<bool> RemoveBookmarkAsync(string userId, int caseId, CancellationToken ct = default);
    Task<bool> IsBookmarkedAsync(string userId, int caseId, CancellationToken ct = default);
    IQueryable<LineOfDutyCase> GetBookmarkedCasesQueryable(string userId);
}
