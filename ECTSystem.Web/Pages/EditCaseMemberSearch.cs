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

    private bool _memberSearchPopupOpen;

    private IList<Member> _memberSearchSelection = [];

    private RadzenTextBox _memberSearchTextBox;

    private Popup _memberSearchPopup;

    private RadzenDataGrid<Member> _memberSearchGrid;

    private async Task OnMemberSearchKeyDown(KeyboardEventArgs args)
    {
        var items = _memberSearch.Results;
        var key = args.Code ?? args.Key;

        if (!args.AltKey && (key == "ArrowDown" || key == "ArrowUp"))
        {
            var result = await JSRuntime.InvokeAsync<int[]>("Radzen.focusTableRow", "member-search-grid", key, _memberSearch.SelectedIndex, null, false);
            _memberSearch.SelectedIndex = result.First();
            var highlighted = _memberSearch.Results.ElementAtOrDefault(_memberSearch.SelectedIndex);
            _memberSearchSelection = highlighted is not null ? [highlighted] : [];
        }
        else if (args.AltKey && key == "ArrowDown" || key == "Enter" || key == "NumpadEnter")
        {
            if (_memberSearchPopupOpen && (key == "Enter" || key == "NumpadEnter"))
            {
                var selected = items.ElementAtOrDefault(_memberSearch.SelectedIndex);
                if (selected != null)
                {
                    await OnMemberSelected(selected);
                    return;
                }
            }

            await _memberSearchPopup.ToggleAsync(_memberSearchTextBox.Element);
            _memberSearchPopupOpen = !_memberSearchPopupOpen;
        }
        else if (key == "Escape" || key == "Tab")
        {
            if (_memberSearchPopupOpen)
            {
                await _memberSearchPopup.CloseAsync();
                _memberSearchPopupOpen = false;
            }
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
            if (_memberSearchPopupOpen)
            {
                await _memberSearchPopup.CloseAsync();
                _memberSearchPopupOpen = false;
            }
            StateHasChanged();
            return;
        }

        if (!_memberSearchPopupOpen)
        {
            await _memberSearchPopup.ToggleAsync(_memberSearchTextBox.Element);
            _memberSearchPopupOpen = true;
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
            var first = _memberSearch.Results.FirstOrDefault();
            _memberSearchSelection = first is not null ? [first] : [];
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
        _memberSearchPopupOpen = false;

        _selectedMemberId = member.Id;

        _viewModel.FirstName = member.FirstName;
        _viewModel.LastName = member.LastName;
        _viewModel.MiddleInitial = member.MiddleInitial;
        _viewModel.OrganizationUnit = member.Unit;
        _viewModel.SSN = member.ServiceNumber;
        _viewModel.DateOfBirth = member.DateOfBirth;
        _viewModel.Component = Regex.Replace(member.Component.ToString(), "(\\B[A-Z])", " $1");

        var parsedRank = LineOfDutyCaseMapper.ParseMilitaryRank(member.Rank);
        _viewModel.Rank = parsedRank.HasValue ? LineOfDutyCaseMapper.FormatRankToFullName(parsedRank.Value) : member.Rank;
        _viewModel.Grade = parsedRank.HasValue ? LineOfDutyCaseMapper.FormatRankToPayGrade(parsedRank.Value): member.Rank;

        _viewModel.MemberName = $"{_viewModel.LastName}, {_viewModel.FirstName}";
        _viewModel.Unit = _viewModel.OrganizationUnit;

        _selectedTabIndex = 0;
        StateHasChanged();
    }
}
