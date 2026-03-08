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
    /// <summary>UI state for the member-search popup.</summary>
    private readonly MemberSearchUiState _memberSearch = new();

    /// <summary>Cancellation source for debouncing member-search API calls.</summary>
    private CancellationTokenSource _searchCts = new();

    /// <summary>Whether the member-search popup is currently open.</summary>
    private bool _memberSearchPopupOpen;

    /// <summary>DataGrid selection binding for the highlighted member row.</summary>
    private IList<Member> _memberSearchSelection = [];

    /// <summary>Reference to the search text box element (used for popup anchoring).</summary>
    private RadzenTextBox _memberSearchTextBox;

    /// <summary>Reference to the member-search popup component.</summary>
    private Popup _memberSearchPopup;

    /// <summary>Reference to the search results data grid.</summary>
    private RadzenDataGrid<Member> _memberSearchGrid;

    /// <summary>
    /// Handles keyboard navigation within the member-search popup.
    /// Arrow keys move the selection, Enter selects, Escape/Tab closes the popup.
    /// </summary>
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

    /// <summary>
    /// Debounces user input and performs a member search via <see cref="IDataService.SearchMembersAsync"/>.
    /// Opens or closes the popup based on input and populates <see cref="MemberSearchUiState.Results"/>.
    /// </summary>
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

    /// <summary>
    /// Populates the Member Information form fields from the selected <paramref name="member"/>
    /// record, closes the search popup, and refreshes the tab display.
    /// </summary>
    /// <param name="member">The member chosen from the search results.</param>
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
        _tabs?.Reload();        StateHasChanged();
    }
}
