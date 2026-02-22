using System.Text.RegularExpressions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class CaseList : ComponentBase, IDisposable
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
    private CancellationTokenSource _loadCts = new();

    private async Task LoadData(LoadDataArgs args)
    {
        // Cancel any previous in-flight request
        await _loadCts.CancelAsync();
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        isLoading = true;

        try
        {
            var result = await CaseService.GetCasesAsync(
                filter: args.Filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                count: true,
                cancellationToken: ct);

            cases = result.Value.AsODataEnumerable();
            count = result.Count;

            await LoadBookmarkStates(ct);
        }
        catch (OperationCanceledException)
        {
            // Request was superseded by a newer one or component disposed — ignore
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

    private async Task LoadBookmarkStates(CancellationToken ct = default)
    {
        bookmarkedCaseIds.Clear();

        if (cases == null) return;

        foreach (var lodCase in cases)
        {
            ct.ThrowIfCancellationRequested();
            if (await CaseService.IsBookmarkedAsync(lodCase.Id, ct))
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

    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
    }
}