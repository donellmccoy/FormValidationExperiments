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

    private async Task RefreshDialogueCommentsAsync()
    {
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

    private static string FormatCommentDateTime(DateTime date)
    {
        return date.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
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
