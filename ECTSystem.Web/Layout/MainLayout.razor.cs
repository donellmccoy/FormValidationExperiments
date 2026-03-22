using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ECTSystem.Web.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; }

    private bool _sidebarExpanded = true;

    protected override async Task OnInitializedAsync()
    {
        BookmarkCountService.OnCountChanged += OnBookmarkCountChanged;
        AuthStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        await BookmarkCountService.RefreshAsync();
    }

    private void OnBookmarkCountChanged() => InvokeAsync(StateHasChanged);

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _ = InvokeAsync(async () =>
        {
            var state = await task;
            if (state.User.Identity?.IsAuthenticated == true)
            {
                await BookmarkCountService.RefreshAsync();
            }
        });
    }

    public void Dispose()
    {
        BookmarkCountService.OnCountChanged -= OnBookmarkCountChanged;
        AuthStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
