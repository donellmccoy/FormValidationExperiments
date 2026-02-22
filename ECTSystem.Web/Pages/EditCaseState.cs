using ECTSystem.Shared.Models;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    private sealed class PageOperationState
    {
        public bool IsLoading { get; set; } = true;
        public bool IsSaving { get; set; }
        public bool IsBusy { get; set; }
        public string BusyMessage { get; set; } = string.Empty;
    }

    private sealed class BookmarkUiState
    {
        public bool IsBookmarked { get; set; }
        public bool IsAnimating { get; set; }
        public string Icon => IsAnimating ? "bookmark_added" : IsBookmarked ? "bookmark_remove" : "bookmark_add";
    }

    private sealed class MemberSearchUiState
    {
        public string Text { get; set; } = string.Empty;
        public List<Member> Results { get; set; } = [];
        public bool IsSearching { get; set; }
        public int SelectedIndex { get; set; }
    }

    private sealed class DocumentUiState
    {
        public bool IsLoading { get; set; }
        public IEnumerable<LineOfDutyDocument> PagedItems { get; set; } = [];
        public int Count { get; set; }
        public string UploadedFileContent { get; set; }
        public string UploadedFileName { get; set; }
        public long? UploadedFileSize { get; set; }
    }
}
