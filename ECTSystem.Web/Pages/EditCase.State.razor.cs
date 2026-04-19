using ECTSystem.Shared.Models;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    /// <summary>
    /// Tracks the overall loading, saving, and busy state of the EditCase page.
    /// </summary>
    private sealed class PageOperationState
    {
        /// <summary>Gets or sets whether the initial case load is in progress.</summary>
        public bool IsLoading { get; set; } = true;

        /// <summary>Gets or sets whether a save operation is in progress.</summary>
        public bool IsSaving { get; set; }

        /// <summary>Gets or sets whether a generic busy overlay should be shown.</summary>
        public bool IsBusy { get; set; }

        /// <summary>Gets or sets the message shown on the busy overlay.</summary>
        public string BusyMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tracks the current bookmark state, animation flag, and computed icon name.
    /// </summary>
    private sealed class BookmarkUiState
    {
        /// <summary>Gets or sets whether the current case is bookmarked.</summary>
        public bool IsBookmarked { get; set; }

        /// <summary>Gets or sets the bookmark ID when bookmarked, or null when not.</summary>
        public int? BookmarkId { get; set; }

        /// <summary>Gets or sets whether a bookmark animation is playing.</summary>
        public bool IsAnimating { get; set; }

        /// <summary>Gets the Material icon name that reflects the current bookmark state.</summary>
        public string Icon => IsAnimating ? "bookmark_added" : IsBookmarked ? "bookmark_remove" : "bookmark_add";
    }

    /// <summary>
    /// Holds the state for the member-search popup, including search text,
    /// result list, and keyboard-navigation index.
    /// </summary>
    private sealed class MemberSearchUiState
    {
        /// <summary>Gets or sets the current search text entered by the user.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the list of members returned from the API search.</summary>
        public List<Member> Results { get; set; } = [];

        /// <summary>Gets or sets whether a search request is in flight.</summary>
        public bool IsSearching { get; set; }

        /// <summary>Gets or sets the keyboard-selected row index in the results grid.</summary>
        public int SelectedIndex { get; set; }
    }

    /// <summary>
    /// Holds the state for the documents tab.
    /// </summary>
    private sealed class DocumentUiState
    {
        /// <summary>Gets or sets whether the document list is loading.</summary>
        public bool IsLoading { get; set; }

        /// <summary>Gets or sets the Bearer token for the RadzenUpload Authorization header.</summary>
        public string AuthToken { get; set; }

        /// <summary>Gets or sets whether a file upload is in progress.</summary>
        public bool IsUploading { get; set; }

        /// <summary>Gets or sets the current upload progress percentage (0–100).</summary>
        public int UploadProgress { get; set; }

        /// <summary>Gets or sets the name of the file currently being uploaded.</summary>
        public string UploadFileName { get; set; }
    }
}
