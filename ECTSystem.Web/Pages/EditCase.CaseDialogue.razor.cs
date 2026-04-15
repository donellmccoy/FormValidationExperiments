using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    [Inject] private ICaseDialogueService CaseDialogueService { get; set; }

    private List<CaseDialogueComment> _dialogueComments = new();
    private string _newCommentText = string.Empty;
    private int? _replyToCommentId;
    private bool _isLoadingComments;
    private int _commentSkip;
    private const int CommentPageSize = 20;
    private bool _hasMoreComments;
    private bool _dialogueLoaded;

    private async Task LoadDialogueCommentsInitialAsync()
    {
        if (_dialogueLoaded) return;
        _dialogueComments.Clear();
        _commentSkip = 0;
        await LoadDialogueCommentsAsync();
        _dialogueLoaded = true;
    }

    private async Task LoadDialogueCommentsAsync()
    {
        _isLoadingComments = true;
        StateHasChanged();

        try
        {
            var result = await CaseDialogueService.GetCommentsAsync(
                _lineOfDutyCase.Id, CommentPageSize, _commentSkip, _cts.Token);
            _dialogueComments.AddRange(result.Items);
            _hasMoreComments = result.TotalCount > _dialogueComments.Count;
        }
        finally
        {
            _isLoadingComments = false;
        }
    }

    private async Task PostCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(_newCommentText)) return;

        var comment = new CaseDialogueComment
        {
            LineOfDutyCaseId = _lineOfDutyCase.Id,
            Text = _newCommentText.Trim(),
            ParentCommentId = _replyToCommentId
        };

        var saved = await CaseDialogueService.PostCommentAsync(comment, _cts.Token);
        _dialogueComments.Insert(0, saved);
        _newCommentText = string.Empty;
        _replyToCommentId = null;
    }

    private async Task AcknowledgeCommentAsync(int commentId)
    {
        await CaseDialogueService.AcknowledgeAsync(commentId, string.Empty, _cts.Token);
        var comment = _dialogueComments.FirstOrDefault(c => c.Id == commentId);
        if (comment != null)
        {
            comment.IsAcknowledged = true;
            comment.AcknowledgedDate = DateTime.UtcNow;
        }
    }

    private void StartReply(int parentId)
    {
        _replyToCommentId = parentId;
    }

    private void CancelReply()
    {
        _replyToCommentId = null;
    }

    private async Task LoadMoreCommentsAsync()
    {
        _commentSkip += CommentPageSize;
        await LoadDialogueCommentsAsync();
    }

    private static string FormatRelativeTime(DateTime date)
    {
        var span = DateTime.UtcNow - date;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 2) return "Yesterday";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return date.ToString("MMM d, yyyy");
    }

    private static string GetAuthorInitials(string authorName)
    {
        if (string.IsNullOrWhiteSpace(authorName)) return "?";
        var parts = authorName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}"
            : authorName[..1];
    }

    private IEnumerable<IGrouping<string, CaseDialogueComment>> GetDateGroupedComments()
    {
        return _dialogueComments
            .Where(c => c.ParentCommentId == null)
            .GroupBy(c => c.CreatedDate.Date == DateTime.Today
                ? $"TODAY, {c.CreatedDate:MMMM d}".ToUpperInvariant()
                : c.CreatedDate.ToString("MMMM d").ToUpperInvariant());
    }

    private IEnumerable<CaseDialogueComment> GetReplies(int parentId)
    {
        return _dialogueComments
            .Where(c => c.ParentCommentId == parentId)
            .OrderBy(c => c.CreatedDate);
    }
}
