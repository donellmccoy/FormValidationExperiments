using System.Text.RegularExpressions;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    private readonly MemberSearchUiState _memberSearch = new();

    private CancellationTokenSource _searchCts = new();

    private RadzenTextBox _memberSearchTextBox;

    private Popup _memberSearchPopup;

    private RadzenDataGrid<Member> _memberSearchGrid;

    private async Task OnMemberSearchKeyDown(KeyboardEventArgs args)
    {
        var items = _memberSearch.Results;
        var popupOpened = await JSRuntime.InvokeAsync<bool>("Radzen.popupOpened", "member-search-popup");
        var key = args.Code ?? args.Key;

        if (!args.AltKey && (key == "ArrowDown" || key == "ArrowUp"))
        {
            var result = await JSRuntime.InvokeAsync<int[]>("Radzen.focusTableRow", "member-search-grid", key, _memberSearch.SelectedIndex, null, false);
            _memberSearch.SelectedIndex = result.First();
        }
        else if (args.AltKey && key == "ArrowDown" || key == "Enter" || key == "NumpadEnter")
        {
            if (popupOpened && (key == "Enter" || key == "NumpadEnter"))
            {
                var selected = items.ElementAtOrDefault(_memberSearch.SelectedIndex);
                if (selected != null)
                {
                    await OnMemberSelected(selected);
                    return;
                }
            }

            await _memberSearchPopup.ToggleAsync(_memberSearchTextBox.Element);
        }
        else if (key == "Escape" || key == "Tab")
        {
            await _memberSearchPopup.CloseAsync();
        }
    }

    private async Task OnMemberSearchInput(ChangeEventArgs args)
    {
        _memberSearch.SelectedIndex = 0;
        _memberSearch.Text = args.Value?.ToString() ?? string.Empty;

        await _searchCts.CancelAsync();
        _searchCts.Dispose();
        _searchCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

        if (string.IsNullOrWhiteSpace(_memberSearch.Text))
        {
            _memberSearch.Results = [];
            StateHasChanged();
            return;
        }

        var token = _searchCts.Token;

        try
        {
            await Task.Delay(300, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _memberSearch.IsSearching = true;
        StateHasChanged();

        try
        {
            _memberSearch.Results = await CaseService.SearchMembersAsync(_memberSearch.Text, token);
        }
        catch (OperationCanceledException)
        {
            // Search was superseded — ignore
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Search Failed", ex.Message);
            _memberSearch.Results = [];
        }
        finally
        {
            _memberSearch.IsSearching = false;
            StateHasChanged();
        }
    }

    private async Task OnMemberSelected(Member member)
    {
        _memberSearch.Text = string.Empty;
        await _memberSearchPopup.CloseAsync();

        _selectedMemberId = member.Id;

        _memberFormModel.FirstName = member.FirstName;
        _memberFormModel.LastName = member.LastName;
        _memberFormModel.MiddleInitial = member.MiddleInitial;
        _memberFormModel.OrganizationUnit = member.Unit;
        _memberFormModel.SSN = member.ServiceNumber;
        _memberFormModel.DateOfBirth = member.DateOfBirth;
        _memberFormModel.Component = Regex.Replace(member.Component.ToString(), "(\\B[A-Z])", " $1");

        var parsedRank = LineOfDutyCaseMapper.ParseMilitaryRank(member.Rank);
        _memberFormModel.Rank = parsedRank.HasValue ? LineOfDutyCaseMapper.FormatRankToFullName(parsedRank.Value) : member.Rank;
        _memberFormModel.Grade = parsedRank.HasValue ? LineOfDutyCaseMapper.FormatRankToPayGrade(parsedRank.Value): member.Rank;

        _caseInfo.MemberName = $"{_memberFormModel.LastName}, {_memberFormModel.FirstName}";
        _caseInfo.Component = _memberFormModel.Component;
        _caseInfo.Rank = _memberFormModel.Rank;
        _caseInfo.Grade = _memberFormModel.Grade;
        _caseInfo.Unit = _memberFormModel.OrganizationUnit;

        _selectedTabIndex = 0;
        StateHasChanged();
    }
}
