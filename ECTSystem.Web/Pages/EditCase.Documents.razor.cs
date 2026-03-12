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
    private int DocumentCount => _documentsCount;

    /// <summary>
    /// Server-side data load handler for the Documents grid.
    /// </summary>
    private async Task LoadDocumentsData(LoadDataArgs args)
    {
        if (_lineOfDutyCase?.Id is null or 0)
        {
            return;
        }

        var generation = ++_documentsLoadGeneration;

        _documents.IsLoading = true;

        try
        {
            var filter = CombineDocumentsFilters(args.Filter, BuildDocumentsSearchFilter(_documentsSearchText));

            var result = await CaseService.GetDocumentsAsync(
                caseId: _lineOfDutyCase.Id,
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: !string.IsNullOrEmpty(args.OrderBy) ? args.OrderBy : "UploadDate desc,Id desc",
                count: true,
                cancellationToken: _cts.Token);

            if (generation != _documentsLoadGeneration)
            {
                return;
            }

            _documentsData = result?.Value?.AsODataEnumerable();
            _documentsCount = result?.Count ?? 0;
        }
        catch (OperationCanceledException)
        {
            // Component disposed — ignore
        }
        catch (Exception ex)
        {
            if (generation == _documentsLoadGeneration)
            {
                Logger.LogWarning(ex, "Failed to load documents for case {CaseId}", _lineOfDutyCase.Id);
                _documentsData = null;
                _documentsCount = 0;
            }
        }
        finally
        {
            if (generation == _documentsLoadGeneration)
            {
                _documents.IsLoading = false;
            }
        }
    }

    private static string BuildDocumentsSearchFilter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Replace("'", "''");
        return $"contains(FileName,'{escaped}') or contains(DocumentType,'{escaped}') or contains(CreatedBy,'{escaped}') or contains(Description,'{escaped}')";
    }

    private static string CombineDocumentsFilters(string columnFilter, string searchFilter)
    {
        var parts = new[] { columnFilter, searchFilter }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => $"({p})")
            .ToList();

        return parts.Count > 0 ? string.Join(" and ", parts) : null;
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Progress</c> event to show loading state during file upload.
    /// </summary>
    private void OnUploadProgress(UploadProgressArgs args)
    {
        _documents.IsLoading = true;
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Complete</c> event. Deserializes the server
    /// response for notification, then reloads the grid from the server.
    /// </summary>
    private void OnUploadComplete(UploadCompleteEventArgs args)
    {
        _documents.IsLoading = false;

        try
        {
            var documents = JsonSerializer.Deserialize<List<LineOfDutyDocument>>(args.RawResponse, JsonOptions);
            if (documents is { Count: > 0 })
            {
                _documentsGrid?.Reload();

                var names = string.Join(", ", documents.Select(d => d.FileName));
                NotificationService.Notify(NotificationSeverity.Success, "Uploaded",
                    documents.Count == 1
                        ? $"\"{documents[0].FileName}\" was added."
                        : $"{documents.Count} files were added: {names}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process upload response");
            NotificationService.Notify(NotificationSeverity.Error, "Upload Error",
                "Files were uploaded but the response could not be processed.");
        }
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Error</c> event.
    /// </summary>
    private void OnUploadError(UploadErrorEventArgs args)
    {
        _documents.IsLoading = false;
        NotificationService.Notify(NotificationSeverity.Error, "Upload Failed", args.Message);
    }

    /// <summary>
    /// Reloads the documents grid from the server.
    /// </summary>
    private async Task RefreshDocumentsAsync()
    {
        if (_lineOfDutyCase?.Id is null or 0)
        {
            return;
        }

        if (_documentsGrid is not null)
        {
            await _documentsGrid.FirstPage(true);
        }
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

    private async Task OnViewDocumentInBrowserAsync(LineOfDutyDocument doc)
    {
        try
        {
            var url = GetDocumentDownloadUrl(doc);
            var response = await Http.GetAsync(url, _cts.Token);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(_cts.Token);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            await JSRuntime.InvokeVoidAsync("openFileInNewTab", contentType, bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to view document {DocumentId}", doc.Id);
            NotificationService.Notify(NotificationSeverity.Error, "View Failed", ex.Message);
        }
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

        if (_documentsGrid is not null)
        {
            await _documentsGrid.FirstPage(true);
        }
    }



    /// <summary>
    /// Confirms and deletes the specified document via
    /// <see cref="IDataService.DeleteDocumentAsync"/>, then refreshes the paged list.
    /// </summary>
    private async Task OnDeleteDocumentAsync(LineOfDutyDocument doc)
    {
        if (_lineOfDutyCase?.Id is null or 0)
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

            _documentsGrid?.Reload();

            NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"\"{doc.FileName}\" was removed.");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", ex.Message);
        }
    }
}
