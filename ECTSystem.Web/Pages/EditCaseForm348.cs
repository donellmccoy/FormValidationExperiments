using Microsoft.JSInterop;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Partial class for EditCase handling AF Form 348 PDF generation and display.
/// </summary>
public partial class EditCase
{
    private const int DocumentsTabIndex = 13;
    private const int Form348TabIndex = 14;
    private const int TrackingTabIndex = 16;
    private string form348BlobUrl;
    private bool isLoadingForm348;
    private string form348Error;

    private bool form348Loaded;

    private async Task OnTabIndexChanged(int index)
    {
        _selectedTabIndex = index;
        if (index == DocumentsTabIndex)
        {
            _documentsGrid?.Reload();
        }
        else if (index == Form348TabIndex && !form348Loaded)
        {
            await LoadForm348Async();
        }
        else if (index == TrackingTabIndex)
        {
            await RefreshTrackingGrid();
        }
    }

    private async Task LoadForm348Async()
    {
        if (_lineOfDutyCase?.Id is null or 0)
        {
            return;
        }

        isLoadingForm348 = true;
        form348Error = null;
        form348Loaded = false;
        StateHasChanged();

        try
        {
            // Revoke any previously created blob URL
            if (!string.IsNullOrEmpty(form348BlobUrl))
            {
                await JSRuntime.InvokeVoidAsync("pdfViewerInterop.revokeBlobUrl", form348BlobUrl);
                form348BlobUrl = null;
            }

            var pdfBytes = await CaseService.GetForm348PdfAsync(_lineOfDutyCase.Id, _cts.Token);
            var base64 = Convert.ToBase64String(pdfBytes);

            // Pass iframe selector so JS sets the src directly — works around
            // RadzenTabs Client-mode not re-rendering inactive panel content.
            form348BlobUrl = await JSRuntime.InvokeAsync<string>(
                "pdfViewerInterop.createBlobUrl", base64, ".form348-iframe");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to generate Form 348 for case {CaseId}", _lineOfDutyCase.Id);
            form348Error = "Unable to generate Form 348. Please try again.";
        }
        finally
        {
            isLoadingForm348 = false;
            form348Loaded = string.IsNullOrEmpty(form348Error);
            StateHasChanged();
        }
    }
}
