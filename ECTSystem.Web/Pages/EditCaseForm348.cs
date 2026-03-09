using Microsoft.JSInterop;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Partial class for EditCase handling AF Form 348 PDF generation and display.
/// </summary>
public partial class EditCase
{
    private const int Form348TabIndex = 14;
    private string form348BlobUrl;
    private bool isLoadingForm348;
    private string form348Error;

    private async Task OnTabChanged(int index)
    {
        _selectedTabIndex = index;
        if (index == Form348TabIndex)
        {
            await LoadForm348Async();
        }
    }

    private async Task LoadForm348Async()
    {
        if (_lineOfDutyCase?.Id is null or 0)
            return;

        isLoadingForm348 = true;
        form348Error = null;
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
            form348BlobUrl = await JSRuntime.InvokeAsync<string>("pdfViewerInterop.createBlobUrl", base64);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to generate Form 348 for case {CaseId}", _lineOfDutyCase.Id);
            form348Error = "Unable to generate Form 348. Please try again.";
        }
        finally
        {
            isLoadingForm348 = false;
            StateHasChanged();
        }
    }
}
