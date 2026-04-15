using Microsoft.JSInterop;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Partial class for EditCase handling AF Form 348 PDF generation and display.
/// </summary>
public partial class EditCase
{
    private const int OuterDocumentsTabIndex = 3;
    private const int OuterForm348TabIndex = 4;
    private const int OuterCaseHistoryTabIndex = 5;
    private const int OuterTrackingTabIndex = 6;

    private int _selectedOuterTabIndex;
    private string form348DataUrl;
    private bool isLoadingForm348;
    private string form348Error;
    private bool _isPrintingCase;

    private bool form348Loaded;

    private void OnTabIndexChanged(int index)
    {
        _selectedTabIndex = index;
    }

    private async Task OnOuterTabIndexChanged(int index)
    {
        _selectedOuterTabIndex = index;

        if (index == OuterDocumentsTabIndex)
        {
            _documentsGrid?.Reload();
            await TryFocusAsync(_documentsSearchBox);
        }
        else if (index == OuterForm348TabIndex && !form348Loaded)
        {
            await LoadForm348Async();
        }
        else if (index == OuterCaseHistoryTabIndex)
        {
            _previousCasesGrid?.Reload();
            await TryFocusAsync(_previousCasesSearchBox);
        }
        else if (index == OuterTrackingTabIndex)
        {
            await RefreshTrackingGrid();
            await TryFocusAsync(_trackingSearchBox);
        }
    }

    private async Task TryFocusAsync(Radzen.FormComponent<string> component)
    {
        if (component is null)
        {
            return;
        }

        try
        {
            await Task.Yield();
            await component.FocusAsync();
        }
        catch (JSException)
        {
            // Element may not be rendered yet; focus is non-critical.
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
        form348DataUrl = null;
        StateHasChanged();

        try
        {
            var pdfBytes = await DocumentService.GetForm348PdfAsync(_lineOfDutyCase.Id, _cts.Token);
            var base64 = Convert.ToBase64String(pdfBytes);
            form348DataUrl = $"data:application/pdf;base64,{base64}#zoom=100";
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
