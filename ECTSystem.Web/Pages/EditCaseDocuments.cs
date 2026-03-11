using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using System.Text.Json;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    /// <summary>
    /// Gets the total number of documents attached to the current case.
    /// </summary>
    private int DocumentCount => _lineOfDutyCase?.Documents?.Count ?? 0;

    /// <summary>
    /// Returns the case's documents filtered by the search text, sorted by upload date (descending), then by ID.
    /// </summary>
    private IEnumerable<LineOfDutyDocument> SortedDocuments
    {
        get
        {
            var docs = _lineOfDutyCase?.Documents?.AsEnumerable() ?? Enumerable.Empty<LineOfDutyDocument>();

            if (!string.IsNullOrWhiteSpace(_documentsSearchText))
            {
                var search = _documentsSearchText.Trim();
                docs = docs.Where(d =>
                    d.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.DocumentType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.CreatedBy.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            return docs
                .OrderByDescending(d => d.UploadDate ?? (d.CreatedDate == default ? DateTime.MinValue : d.CreatedDate))
                .ThenByDescending(d => d.Id);
        }
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Complete</c> event. Deserializes the server
    /// response into a <see cref="LineOfDutyDocument"/> and adds it to the case.
    /// </summary>
    private void OnUploadComplete(UploadCompleteEventArgs args)
    {
        try
        {
            var document = JsonSerializer.Deserialize<LineOfDutyDocument>(args.RawResponse, JsonOptions);
            if (document is not null)
            {
                _lineOfDutyCase.Documents ??= new HashSet<LineOfDutyDocument>();
                _lineOfDutyCase.Documents.Add(document);
                _documentsGrid?.Reload();
                StateHasChanged();

                NotificationService.Notify(NotificationSeverity.Success, "Uploaded",
                    $"\"{document.FileName}\" was added.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process upload response");
            NotificationService.Notify(NotificationSeverity.Error, "Upload Error",
                "File was uploaded but the response could not be processed.");
        }
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Error</c> event.
    /// </summary>
    private void OnUploadError(UploadErrorEventArgs args)
    {
        NotificationService.Notify(NotificationSeverity.Error, "Upload Failed", args.Message);
    }

    /// <summary>
    /// Refreshes the documents list by reloading the case's documents from
    /// the in-memory collection and resetting the paged data.
    /// </summary>
    private void RefreshDocumentsList()
    {
        _documentsGrid?.Reload();
        StateHasChanged();
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
    /// Downloads the document via an authenticated HTTP request and triggers
    /// a browser file-save using a temporary blob URL.
    /// </summary>
    private async Task OnDownloadDocumentAsync(LineOfDutyDocument doc)
    {
        try
        {
            var url = GetDocumentDownloadUrl(doc);
            var response = await Http.GetAsync(url, _cts.Token);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(_cts.Token);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            await JSRuntime.InvokeVoidAsync("downloadFileFromBytes", doc.FileName, contentType, bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to download document {DocumentId}", doc.Id);
            NotificationService.Notify(NotificationSeverity.Error, "Download Failed", ex.Message);
        }
    }

    private async Task OnDocumentsSearchInput(ChangeEventArgs args)
    {
        _documentsSearchText = args.Value?.ToString() ?? string.Empty;

        await _documentsSearchCts.CancelAsync();
        _documentsSearchCts.Dispose();
        _documentsSearchCts = new CancellationTokenSource();
        var token = _documentsSearchCts.Token;

        try
        {
            await Task.Delay(300, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _documentsGrid?.Reload();
        StateHasChanged();
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

            // Remove by ID rather than reference equality so stale references
            // (e.g. after a case save/refresh) still match.
            var toRemove = _lineOfDutyCase.Documents?.FirstOrDefault(d => d.Id == doc.Id);
            if (toRemove is not null)
            {
                _lineOfDutyCase.Documents!.Remove(toRemove);
            }

            _documentsGrid?.Reload();

            NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"\"{doc.FileName}\" was removed.");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", ex.Message);
        }
    }
}
