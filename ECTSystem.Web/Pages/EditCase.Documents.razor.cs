using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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
    /// Fetches only the document count for the badge without loading document rows.
    /// </summary>
    private async Task LoadDocumentCountAsync(int caseId)
    {
        try
        {
            var result = await DocumentService.GetDocumentsAsync(
                caseId: caseId,
                top: 0,
                count: true,
                cancellationToken: _cts.Token);

            _documentsCount = result?.Count ?? 0;
        }
        catch (OperationCanceledException)
        {
            // Ignore — component disposed
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load document count for case {CaseId}", caseId);
        }
    }

    /// <summary>
    /// Maps user identifiers to their corresponding display names.
    /// </summary>
    private readonly Dictionary<string, string> _userDisplayNames = new();

    /// <summary>
    /// Gets the display name for the specified user identifier.
    /// </summary>
    /// <param name="userId">The user identifier to retrieve the display name for.</param>
    /// <returns>The user's display name if found; otherwise, the user identifier itself. Returns an empty string if <paramref
    /// name="userId"/> is null or empty.</returns>
    private string GetUserDisplayName(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return string.Empty;
        }

        return _userDisplayNames.GetValueOrDefault(userId, userId);
    }

    /// <summary>
    /// Server-side data load handler for the Documents grid.
    /// </summary>
    private async Task LoadDocumentsData(LoadDataArgs args)
    {
        if (_lineOfDutyCase?.Id is null or 0)
        {
            return;
        }

        // Cancel any previous in-flight request
        await _documentsLoadCts.CancelAsync();
        _documentsLoadCts.Dispose();
        _documentsLoadCts = new CancellationTokenSource();
        var ct = _documentsLoadCts.Token;

        _documents.IsLoading = true;

        try
        {
            var filter = CombineDocumentsFilters(args.Filter, BuildDocumentsSearchFilter(_documentsSearchText));

            var result = await DocumentService.GetDocumentsAsync(
                caseId: _lineOfDutyCase.Id,
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: !string.IsNullOrEmpty(args.OrderBy) ? args.OrderBy : "UploadDate desc,Id desc",
                count: true,
                cancellationToken: ct);

            _documentsData = result?.Value?.AsODataEnumerable();
            _documentsCount = result?.Count ?? 0;

            if (_documentsData is not null)
            {
                var userIds = _documentsData
                    .Select(d => d.CreatedBy)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                if (userIds.Count > 0)
                {
                    var names = await UserService.GetDisplayNamesAsync(userIds, ct);
                    foreach (var kvp in names)
                    {
                        _userDisplayNames[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("LoadDocumentsData cancelled — superseded by newer request");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load documents for case {CaseId}", _lineOfDutyCase.Id);
            _documentsData = null;
            _documentsCount = 0;
        }
        finally
        {
            _documents.IsLoading = false;

            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Builds an OData filter expression for document search across FileName, Description, and DocumentType fields.
    /// </summary>
    /// <remarks>Single quotes in the search text are escaped for OData syntax. DocumentType enum values are
    /// matched by their display names.</remarks>
    /// <param name="text">The search text to use for filtering.</param>
    /// <returns>An OData filter string combining multiple field searches, or null if the search text is empty.</returns>
    private static string BuildDocumentsSearchFilter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Replace("'", "''");

        var parts = new List<string>
        {
            $"contains(FileName,'{escaped}')",
            $"contains(Description,'{escaped}')"
        };

        // DocumentType is an enum — match any member whose display name contains the search text
        var matchingTypes = Enum.GetValues<DocumentType>()
            .Where(dt => dt.ToDisplayString().Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingTypes.Count == 1)
        {
            parts.Add($"DocumentType eq '{matchingTypes[0]}'");
        }
        else if (matchingTypes.Count > 1)
        {
            var typeFilters = matchingTypes.Select(dt => $"DocumentType eq '{dt}'");
            parts.Add($"({string.Join(" or ", typeFilters)})");
        }

        return string.Join(" or ", parts);
    }

    /// <summary>
    /// Combines column and search filter expressions into a single filter string using the logical AND operator.
    /// </summary>
    /// <param name="columnFilter">Filter expression for column-based filtering.</param>
    /// <param name="searchFilter">Filter expression for search-based filtering.</param>
    /// <returns>A combined filter string with non-empty filters joined by " and ", or <see langword="null"/> if both filters are
    /// empty.</returns>
    private static string CombineDocumentsFilters(string columnFilter, string searchFilter)
    {
        var parts = new[] { columnFilter, searchFilter }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => $"({p})")
            .ToList();

        return parts.Count > 0 ? string.Join(" and ", parts) : null;
    }

    /// <summary>
    /// Re-reads the access token from local storage and updates the cached
    /// <c>Authorization</c> header value used by <see cref="Radzen.Blazor.RadzenUpload"/>.
    /// Required because the <see cref="Handlers.AuthorizationMessageHandler"/> can refresh
    /// the JWT silently on any HttpClient 401, leaving the cached upload header stale.
    /// </summary>
    private async Task RefreshUploadAuthTokenAsync()
    {
        try
        {
            var token = await LocalStorage.GetItemAsStringAsync("accessToken");
            _documents.AuthToken = !string.IsNullOrEmpty(token) ? $"Bearer {token}" : string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to refresh auth token for document uploads");
        }
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Change</c> event (fired when files are selected).
    /// Refreshes the cached Authorization header so the immediately-following XHR
    /// upload (Auto="true") sends the most recent access token.
    /// </summary>
    private async Task OnUploadChange(UploadChangeEventArgs args)
    {
        await RefreshUploadAuthTokenAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Progress</c> event to show upload progress.
    /// </summary>
    private void OnUploadProgress(UploadProgressArgs args)
    {
        _documents.IsUploading = true;
        _documents.UploadProgress = (int)args.Progress;
        _documents.UploadFileName = args.Files?.FirstOrDefault()?.Name;
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Complete</c> event. Deserializes the server
    /// response and optimistically prepends the new documents to the local
    /// collection, avoiding a full grid reload round-trip.
    /// </summary>
    private void OnUploadComplete(UploadCompleteEventArgs args)
    {
        _documents.IsLoading = false;
        _documents.IsUploading = false;
        _documents.UploadProgress = 0;
        _documents.UploadFileName = null;

        try
        {
            var documents = ParseUploadResponse(args.RawResponse);

            if (documents is { Count: > 0 })
            {
                // Optimistic update: prepend uploaded documents to the local collection
                // and bump the count, eliminating a follow-up GET round-trip.
                var existing = _documentsData?.Cast<LineOfDutyDocument>().ToList() ?? [];
                _documentsData = documents.Concat(existing).AsODataEnumerable();
                _documentsCount += documents.Count;

                var names = string.Join(", ", documents.Select(d => d.FileName));
                NotificationService.Notify(NotificationSeverity.Success, "Uploaded",
                    documents.Count == 1
                        ? $"\"{documents[0].FileName}\" was added."
                        : $"{documents.Count} files were added: {names}");

                InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process upload response — falling back to grid reload");
            _documentsGrid?.Reload();
            NotificationService.Notify(NotificationSeverity.Error, "Upload Error",
                "Files were uploaded but the response could not be processed.");
        }
    }

    /// <summary>
    /// Parses the upload endpoint response, which may be OData-formatted
    /// (<c>{"value":[...]}</c>), a plain JSON array, or a single object.
    /// </summary>
    private List<LineOfDutyDocument> ParseUploadResponse(string rawResponse)
    {
        using var doc = JsonDocument.Parse(rawResponse);

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("value", out var valueElement))
        {
            return JsonSerializer.Deserialize<List<LineOfDutyDocument>>(valueElement.GetRawText(), JsonOptions) ?? [];
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<LineOfDutyDocument>>(rawResponse, JsonOptions) ?? [];
        }

        var single = JsonSerializer.Deserialize<LineOfDutyDocument>(rawResponse, JsonOptions);
        return single is not null ? [single] : [];
    }

    /// <summary>
    /// Handles the <c>RadzenUpload.Error</c> event.
    /// </summary>
    private void OnUploadError(UploadErrorEventArgs args)
    {
        _documents.IsLoading = false;
        _documents.IsUploading = false;
        _documents.UploadProgress = 0;
        _documents.UploadFileName = null;

        var detail = !string.IsNullOrWhiteSpace(args.Message) ? args.Message : "The server did not return details. Check that you are signed in and try again.";
        Logger.LogError("Document upload failed: {Message}", args.Message);
        NotificationService.Notify(NotificationSeverity.Error, "Upload Failed", detail);
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
        return $"{Http.BaseAddress}odata/Documents({doc.Id})/$value";
    }

    /// <summary>
    /// Downloads and opens a Line of Duty document in a new browser tab.
    /// </summary>
    /// <param name="doc">The document to view in the browser.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Handles search input changes for the documents grid with a 700ms debounce delay.
    /// </summary>
    /// <param name="args">The change event arguments containing the new search input value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnDocumentsSearchInput(ChangeEventArgs args)
    {
        _documentsSearchText = args.Value?.ToString() ?? string.Empty;

        await _documentsSearchCts.CancelAsync();
        _documentsSearchCts.Dispose();
        _documentsSearchCts = new CancellationTokenSource();
        var token = _documentsSearchCts.Token;

        try
        {
            await Task.Delay(700, token);
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
    /// <see cref="IDocumentService.DeleteDocumentAsync"/>, then refreshes the paged list.
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
            await DocumentService.DeleteDocumentAsync(_lineOfDutyCase.Id, doc.Id, _cts.Token);

            if (_documentsCount > 0)
            {
                _documentsCount--;
            }

            if (_documentsGrid is not null)
            {
                await _documentsGrid.Reload();
            }

            NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"\"{doc.FileName}\" was removed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex?.GetBaseException()?.Message);
            NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", ex.Message);
        }
    }
}
