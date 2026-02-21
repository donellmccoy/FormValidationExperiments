using System.Text.RegularExpressions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class CaseList : ComponentBase
{
    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private ContextMenuService ContextMenuService { get; set; }

    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    private ODataEnumerable<LineOfDutyCase> cases;
    private IList<LineOfDutyCase> _selectedCases = [];
    private HashSet<int> bookmarkedCaseIds = [];
    private int count;
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadData(new LoadDataArgs { Skip = 0, Top = 10 });
    }

    private async Task LoadData(LoadDataArgs args)
    {
        isLoading = true;

        try
        {
            var result = await CaseService.GetCasesAsync(
                filter: args.Filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                count: true);

            cases = result.Value.AsODataEnumerable();
            count = result.Count;

            await LoadBookmarkStates();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cases: {ex}");
            cases = null;
            count = 0;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadBookmarkStates()
    {
        bookmarkedCaseIds.Clear();

        if (cases == null) return;

        foreach (var lodCase in cases)
        {
            if (await CaseService.IsBookmarkedAsync(lodCase.Id))
            {
                bookmarkedCaseIds.Add(lodCase.Id);
            }
        }
    }

    private async Task ToggleBookmark(LineOfDutyCase lodCase)
    {
        var isBookmarked = bookmarkedCaseIds.Contains(lodCase.Id);

        if (isBookmarked)
        {
            await CaseService.RemoveBookmarkAsync(lodCase.Id);
            bookmarkedCaseIds.Remove(lodCase.Id);
            NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
        }
        else
        {
            await CaseService.AddBookmarkAsync(lodCase.Id);
            bookmarkedCaseIds.Add(lodCase.Id);
            NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);
        }

        await BookmarkCountService.RefreshAsync();
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }

    private void OnCreateCase()
    {
        Navigation.NavigateTo("/case/new");
    }

    private void OnRowContextMenu(DataGridRowMouseEventArgs<LineOfDutyCase> args)
    {
        _ = ShowContextMenuAsync(args);
    }

    private async Task ShowContextMenuAsync(DataGridRowMouseEventArgs<LineOfDutyCase> args)
    {
        var lodCase = args.Data;
        var isBookmarked = await CaseService.IsBookmarkedAsync(lodCase.Id);

        ContextMenuService.Open(args,
            [
                new ContextMenuItem
                {
                    Text = isBookmarked ? "Remove Bookmark" : "Add Bookmark",
                    Icon = isBookmarked ? "bookmark_remove" : "bookmark_add"
                }
            ],
            async _ =>
            {
                if (isBookmarked)
                {
                    await CaseService.RemoveBookmarkAsync(lodCase.Id);
                    NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
                }
                else
                {
                    await CaseService.AddBookmarkAsync(lodCase.Id);
                    NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);
                }

                await BookmarkCountService.RefreshAsync();
            });
    }
}