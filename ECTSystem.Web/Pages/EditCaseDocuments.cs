using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.JSInterop;
using Radzen;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    private int DocumentCount => _lodCase?.Documents?.Count ?? 0;

    private IEnumerable<LineOfDutyDocument> SortedDocuments =>
        _lodCase?.Documents?
            .OrderByDescending(d => d.UploadDate ?? (d.CreatedDate == default ? DateTime.MinValue : d.CreatedDate))
            .ThenByDescending(d => d.Id) ?? Enumerable.Empty<LineOfDutyDocument>();

    private async Task OnFileSelected(string content)
    {
        _documents.UploadedFileContent = content;

        if (!string.IsNullOrWhiteSpace(_documents.UploadedFileContent) && !string.IsNullOrWhiteSpace(_documents.UploadedFileName))
        {
            await AddDocumentAsync();
        }
    }

    private async Task AddDocumentAsync()
    {
        if (string.IsNullOrWhiteSpace(_documents.UploadedFileContent) || string.IsNullOrWhiteSpace(_documents.UploadedFileName))
        {
            return;
        }

        if (_lodCase?.Id is null or 0)
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Save Case First",
                "Please save the case before uploading documents.");
            return;
        }

        _lodCase.Documents ??= new HashSet<LineOfDutyDocument>();

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
                _lodCase.Id, _documents.UploadedFileName, contentType, fileBytes, _cts.Token);

            if (saved is not null)
            {
                _lodCase.Documents.Add(saved);
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

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1048576 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / 1048576.0:F1} MB"
        };
    }

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

    private string GetDocumentDownloadUrl(LineOfDutyDocument doc)
    {
        return $"{Http.BaseAddress}api/cases/{_lodCase.Id}/documents/{doc.Id}/download";
    }

    private async Task OnDownloadDocumentAsync(LineOfDutyDocument doc)
    {
        var url = GetDocumentDownloadUrl(doc);
        await JSRuntime.InvokeVoidAsync("open", url, "_blank");
    }

    private void OnDocumentsLoadData(LoadDataArgs args)
    {
        _documents.IsLoading = true;

        var sorted = SortedDocuments.ToList();
        _documents.Count = sorted.Count;
        _documents.PagedItems = sorted.Skip(args.Skip ?? 0).Take(args.Top ?? 10);

        _documents.IsLoading = false;
    }

    private async Task OnDeleteDocumentAsync(LineOfDutyDocument doc)
    {
        if (_lodCase?.Id is null or 0 || doc.Id == 0)
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
            await CaseService.DeleteDocumentAsync(_lodCase.Id, doc.Id, _cts.Token);
            _lodCase.Documents?.Remove(doc);

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
