using ECTSystem.Shared.Enums;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using Radzen;

namespace ECTSystem.Web.Pages;

public partial class CreateCase : ComponentBase
{
    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private NavigationManager NavigationManager { get; set; }

    private bool isLoading = false;

    private bool isBusy;
    private string busyMessage = string.Empty;

    private string memberSearchText = string.Empty;
    private string dodId = string.Empty;

    private IncidentType incidentType;
    private LineOfDutyProcessType processType;

    private MemberInfoFormModel memberFormModel = new();

    private static readonly IncidentType[] incidentTypes = Enum.GetValues<IncidentType>();
    private static readonly LineOfDutyProcessType[] processTypes = Enum.GetValues<LineOfDutyProcessType>();
    private static readonly MilitaryRank[] militaryRanks = Enum.GetValues<MilitaryRank>();
    private static readonly MemberStatus[] memberStatuses = Enum.GetValues<MemberStatus>();

    private async Task OnSubmit(MemberInfoFormModel model)
    {
        isBusy = true;
        busyMessage = "Creating case...";
        StateHasChanged();

        try
        {
            var newCase = new ECTSystem.Shared.Models.LineOfDutyCase
            {
                IncidentType = incidentType,
                ProcessType = processType
            };
            ECTSystem.Shared.Mapping.LineOfDutyCaseMapper.ApplyMemberInfo(memberFormModel, newCase);

            var saved = await CaseService.SaveCaseAsync(newCase);

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Case Created",
                Detail = "New LOD case has been created successfully.",
                Duration = 3000
            });

            NavigationManager.NavigateTo($"/case/{saved.Id}");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Create Failed",
                Detail = ex.Message,
                Duration = 5000
            });
        }
        finally
        {
            isBusy = false;
            StateHasChanged();
        }
    }

    private async Task OnMemberSearch()
    {
        if (string.IsNullOrWhiteSpace(memberSearchText))
        {
            NotificationService.Notify(NotificationSeverity.Info, "Search", "Please enter a name or last 4 SSN.");
            return;
        }

        isBusy = true;
        busyMessage = "Searching for member...";
        StateHasChanged();

        try
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Not Available",
                "Member search is not yet connected to a data source.");
        }
        finally
        {
            isBusy = false;
            StateHasChanged();
        }
    }

    private async Task OnMemberSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await OnMemberSearch();
        }
    }

    private void OnCancel()
    {
        NavigationManager.NavigateTo("/");
    }

    private void OnSsnChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            memberFormModel.SSN = string.Empty;
            return;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > 4)
        {
            digits = digits[..4];
        }

        memberFormModel.SSN = digits;
        StateHasChanged();
    }
}
