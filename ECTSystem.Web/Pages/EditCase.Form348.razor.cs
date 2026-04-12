using Microsoft.JSInterop;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Partial class for EditCase handling AF Form 348 PDF generation and display.
/// </summary>
public partial class EditCase
{
    private const int DocumentsTabIndex = 13;
    private const int Form348TabIndex = 14;
    private const int CaseHistoryTabIndex = 15;
    private const int TrackingTabIndex = 16;
    private string form348BlobUrl;
    private bool isLoadingForm348;
    private string form348Error;
    private bool _isPrintingCase;

    private bool form348Loaded;

    private async Task OnTabIndexChanged(int index)
    {
        _selectedTabIndex = index;
        if (index == DocumentsTabIndex)
        {
            _documentsGrid?.Reload();
            if (_documentsSearchBox is not null)
            {
                await _documentsSearchBox.FocusAsync();
            }
        }
        else if (index == Form348TabIndex && !form348Loaded)
        {
            await LoadForm348Async();
        }
        else if (index == CaseHistoryTabIndex)
        {
            if (_previousCasesSearchBox is not null)
            {
                await _previousCasesSearchBox.FocusAsync();
            }
        }
        else if (index == TrackingTabIndex)
        {
            await RefreshTrackingGrid();
            if (_trackingSearchBox is not null)
            {
                await _trackingSearchBox.FocusAsync();
            }
        }
    }

    private async Task OnPrintCaseClick()
    {
        if (_lineOfDutyCase?.Id is null or 0)
        {
            return;
        }

        _isPrintingCase = true;
        StateHasChanged();

        try
        {
            var pdfBytes = await DocumentService.GetForm348PdfAsync(_lineOfDutyCase.Id, _cts.Token);
            var base64 = Convert.ToBase64String(pdfBytes);
            await JSRuntime.InvokeVoidAsync("pdfViewerInterop.printPdf", base64);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to print Form 348 for case {CaseId}", _lineOfDutyCase.Id);
            NotificationService.Notify(Radzen.NotificationSeverity.Error, "Print Error",
                "Unable to generate the print preview. Please try again.");
        }
        finally
        {
            _isPrintingCase = false;
            StateHasChanged();
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

            var pdfBytes = await DocumentService.GetForm348PdfAsync(_lineOfDutyCase.Id, _cts.Token);
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
