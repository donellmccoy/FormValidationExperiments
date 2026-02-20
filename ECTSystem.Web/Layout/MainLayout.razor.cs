using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    private bool _sidebarExpanded = true;

    protected override async Task OnInitializedAsync()
    {
        BookmarkCountService.OnCountChanged += OnBookmarkCountChanged;
        await BookmarkCountService.RefreshAsync();
    }

    private void OnBookmarkCountChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        BookmarkCountService.OnCountChanged -= OnBookmarkCountChanged;
    }
}
