using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.JSInterop;
using Radzen;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    /// <summary>
    /// Gets the total number of documents attached to the current case.
    /// </summary>
    private int DocumentCount => _lineOfDutyCase?.Documents?.Count ?? 0;

    /// <summary>
    /// Returns the case’s documents sorted by upload date (descending), then by ID.
    /// </summary>
    private IEnumerable<LineOfDutyDocument> SortedDocuments =>
        _lineOfDutyCase?.Documents?
            .OrderByDescending(d => d.UploadDate ?? (d.CreatedDate == default ? DateTime.MinValue : d.CreatedDate))
            .ThenByDescending(d => d.Id) ?? Enumerable.Empty<LineOfDutyDocument>();

    /// <summary>
    /// Receives the Base64 content from the <c>RadzenFileInput</c>.
    /// The content arrives before the file name, so upload is deferred
    /// until <see cref="OnFileNameChanged"/> fires.
    /// </summary>
    private void OnFileContentChanged(string content)
    {
        _documents.UploadedFileContent = content;
    }

    /// <summary>
    /// Receives the file name from the <c>RadzenFileInput</c> after the content
    /// has been set. Triggers <see cref="AddDocumentAsync"/> when both content
    /// and file name are available.
    /// </summary>
    private async Task OnFileNameChanged(string fileName)
    {
        _documents.UploadedFileName = fileName;

        if (!string.IsNullOrWhiteSpace(_documents.UploadedFileContent) && !string.IsNullOrWhiteSpace(_documents.UploadedFileName))
        {
            await AddDocumentAsync();
        }
    }

    /// <summary>
    /// Decodes the staged Base64 file, sends it to the API via
    /// <see cref="IDataService.UploadDocumentAsync"/>, adds the resulting document
    /// to the case, and refreshes the paged document list.
    /// </summary>
    private async Task AddDocumentAsync()
    {
        if (string.IsNullOrWhiteSpace(_documents.UploadedFileContent) || string.IsNullOrWhiteSpace(_documents.UploadedFileName))
        {
            return;
        }

        if (_lineOfDutyCase?.Id is null or 0)
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Save Case First",
                "Please save the case before uploading documents.");
            return;
        }

        _lineOfDutyCase.Documents ??= new HashSet<LineOfDutyDocument>();

        var contentType = GetContentType(_documents.UploadedFileName);

        try
        {
            // RadzenFileInput returns a base64 data URI (e.g., "data:application/pdf;base64,JVBERi0...")
            var base64Data = _documents.UploadedFileContent;
            if (base64Data.Contains(","))
            {
                base64Data = base64Data.Split(',')[1];
            }
            var fileBytes = Convert.FromBase64String(base64Data);

            var saved = await CaseService.UploadDocumentAsync(
                _lineOfDutyCase.Id, _documents.UploadedFileName, contentType, fileBytes, _cts.Token);

            if (saved is not null)
            {
                _lineOfDutyCase.Documents.Add(saved);
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Upload Failed", ex.Message);
        }

        // Refresh the DataList
        var sorted = SortedDocuments.ToList();
        _documents.Count = sorted.Count;
        _documents.PagedItems = sorted.Take(10);

        // Reset upload fields
        _documents.UploadedFileContent = null;
        _documents.UploadedFileName = null;
        _documents.UploadedFileSize = null;
    }

    /// <summary>
    /// Maps a file extension to its MIME content type.
    /// </summary>
    /// <param name="fileName">The file name whose extension is inspected.</param>
    /// <returns>The MIME type string, defaulting to <c>application/octet-stream</c>.</returns>
    private static string GetContentType(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Formats a byte count as a human-readable size string (B, KB, or MB).
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1048576 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / 1048576.0:F1} MB"
        };
    }

    /// <summary>
    /// Returns the Material icon name appropriate for the file’s extension.
    /// </summary>
    private static string GetDocumentIcon(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "picture_as_pdf",
            ".doc" or ".docx" => "article",
            ".xls" or ".xlsx" => "grid_on",
            ".png" or ".jpg" or ".jpeg" => "image",
            ".txt" => "text_snippet",
            _ => "description"
        };
    }

    /// <summary>
    /// Builds the API download URL for the specified document.
    /// </summary>
    private string GetDocumentDownloadUrl(LineOfDutyDocument doc)
    {
        return $"{Http.BaseAddress}api/cases/{_lineOfDutyCase.Id}/documents/{doc.Id}/download";
    }

    /// <summary>
    /// Opens the document download URL in a new browser tab.
    /// </summary>
    private async Task OnDownloadDocumentAsync(LineOfDutyDocument doc)
    {
        var url = GetDocumentDownloadUrl(doc);
        await JSRuntime.InvokeVoidAsync("open", url, "_blank");
    }

    /// <summary>
    /// Handles paging for the documents <c>RadzenDataList</c>.
    /// Slices <see cref="SortedDocuments"/> using the requested skip/take values.
    /// </summary>
    private void OnDocumentsLoadData(LoadDataArgs args)
    {
        _documents.IsLoading = true;

        var sorted = SortedDocuments.ToList();
        _documents.Count = sorted.Count;
        _documents.PagedItems = sorted.Skip(args.Skip ?? 0).Take(args.Top ?? 10);

        _documents.IsLoading = false;
    }

    /// <summary>
    /// Confirms and deletes the specified document via
    /// <see cref="IDataService.DeleteDocumentAsync"/>, then refreshes the paged list.
    /// </summary>
    private async Task OnDeleteDocumentAsync(LineOfDutyDocument doc)
    {
        if (_lineOfDutyCase?.Id is null or 0 || doc.Id == 0)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Are you sure you want to delete \"{doc.FileName}\"?",
            "Delete Document",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        try
        {
            await CaseService.DeleteDocumentAsync(_lineOfDutyCase.Id, doc.Id, _cts.Token);
            _lineOfDutyCase.Documents?.Remove(doc);

            // Refresh the DataList
            var sorted = SortedDocuments.ToList();
            _documents.Count = sorted.Count;
            _documents.PagedItems = sorted.Take(10);

            NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"\"{doc.FileName}\" was removed.");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", ex.Message);
        }
    }
}
