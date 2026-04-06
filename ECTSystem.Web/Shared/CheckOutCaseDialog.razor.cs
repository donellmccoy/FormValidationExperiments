using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Shared;

public partial class CheckOutCaseDialog : ComponentBase
{
    [Inject]
    private DialogService DialogService { get; set; }

    [Parameter]
    public string CaseId { get; set; }

    private void OnCheckOut(RadzenSplitButtonItem item)
    {
        if (item?.Value == "readonly")
        {
            DialogService.Close("readonly");
        }
        else
        {
            DialogService.Close("checkout");
        }
    }
}
