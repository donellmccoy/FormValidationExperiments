using Microsoft.AspNetCore.Components;
using Radzen;

namespace ECTSystem.Web.Shared;

public partial class CheckOutCaseDialog : ComponentBase
{
    [Inject]
    private DialogService DialogService { get; set; }

    [Parameter]
    public string CaseId { get; set; }
}
