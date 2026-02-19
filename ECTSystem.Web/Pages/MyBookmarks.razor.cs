using System.Text.RegularExpressions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ECTSystem.Web.Pages;

public partial class MyBookmarks : ComponentBase
{
    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    private ODataEnumerable<CaseBookmark> _bookmarks;
    private int _count;
    private bool _isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadData(new LoadDataArgs { Skip = 0, Top = 10 });
    }

    private async Task LoadData(LoadDataArgs args)
    {
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
        try
        {
            await CaseService.RemoveBookmarkAsync(bookmark.LineOfDutyCaseId);
            await LoadData(new LoadDataArgs { Skip = 0, Top = 10 });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing bookmark: {ex}");
        }
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
