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

    [Inject]
    private IdleTimeoutService IdleTimeout { get; set; }

    private bool _sidebarExpanded = true;

    protected override async Task OnInitializedAsync()
    {
        BookmarkCountService.OnCountChanged += OnBookmarkCountChanged;
        AuthStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        await BookmarkCountService.RefreshAsync();

        var initialState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (initialState.User.Identity?.IsAuthenticated == true)
        {
            await IdleTimeout.StartAsync();
        }
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
                await IdleTimeout.StartAsync();
            }
            else
            {
                await IdleTimeout.StopAsync();
            }
        });
    }

    public void Dispose()
    {
        BookmarkCountService.OnCountChanged -= OnBookmarkCountChanged;
        AuthStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        _ = IdleTimeout.StopAsync();
    }
}
