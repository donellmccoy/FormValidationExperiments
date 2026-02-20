using System.Text.RegularExpressions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class MyBookmarks : ComponentBase
{
    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    private RadzenDataGrid<CaseBookmark> _grid;
    private ODataEnumerable<CaseBookmark> _bookmarks;
    private IList<CaseBookmark> _selectedBookmarks = [];
    private int _count;
    private bool _isLoading;
    private LoadDataArgs _lastArgs;

    protected override async Task OnInitializedAsync()
    {
        await LoadData(new LoadDataArgs { Skip = 0, Top = 10 });
    }

    private async Task LoadData(LoadDataArgs args)
    {
        _lastArgs = args;
        _isLoading = true;

        try
        {
            var result = await CaseService.GetBookmarkedCasesAsync(
                filter: args.Filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                count: true);

            _bookmarks = result.Value.AsODataEnumerable();
            _count = result.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading bookmarks: {ex}");
            _bookmarks = null;
            _count = 0;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnRemoveBookmark(CaseBookmark bookmark)
    {
        var confirmed = await DialogService.Confirm(
            $"Remove bookmark for case {bookmark.LineOfDutyCase?.CaseId}?",
            "Remove Bookmark",
            new ConfirmOptions { OkButtonText = "Remove", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        try
        {
            await CaseService.RemoveBookmarkAsync(bookmark.LineOfDutyCaseId);
            await LoadData(_lastArgs ?? new LoadDataArgs { Skip = 0, Top = 10 });
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing bookmark: {ex}");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to remove bookmark. Please try again.");
        }
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
