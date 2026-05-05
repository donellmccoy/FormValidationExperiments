#nullable enable
using System.IdentityModel.Tokens.Jwt;
using Blazored.LocalStorage;
using ECTSystem.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Radzen;

namespace ECTSystem.Web.Services;

/// <summary>
/// Tracks user activity via JS interop and signs the user out after a configurable
/// period of inactivity. Also schedules a hard logout at the JWT <c>exp</c> claim
/// (whichever comes first) and broadcasts logout/activity across browser tabs so
/// every open tab stays in sync.
/// </summary>
public sealed class IdleTimeoutService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly IAuthService _authService;
    private readonly DialogService _dialog;
    private readonly ILocalStorageService _localStorage;
    private readonly IdleTimeoutOptions _options;

    private IJSObjectReference? _module;
    private DotNetObjectReference<IdleTimeoutService>? _selfRef;
    private System.Threading.Timer? _warningTimer;
    private System.Threading.Timer? _logoutTimer;
    private System.Threading.Timer? _jwtExpiryTimer;
    private bool _started;
    private bool _warningOpen;
    private DateTime? _jwtExpiresAtUtc;

    public IdleTimeoutService(
        IJSRuntime js,
        IAuthService authService,
        DialogService dialog,
        ILocalStorageService localStorage,
        IOptions<IdleTimeoutOptions> options)
    {
        _js = js;
        _authService = authService;
        _dialog = dialog;
        _localStorage = localStorage;
        _options = options.Value;
    }

    public async Task StartAsync()
    {
        if (_started) { return; }
        _started = true;

        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/idleMonitor.js");
        _selfRef ??= DotNetObjectReference.Create(this);

        await ScheduleJwtExpiryAsync();
        ResetIdleTimers();

        await _module.InvokeVoidAsync("start", _selfRef, (int)_options.ActivityThrottle.TotalMilliseconds);
    }

    public async Task StopAsync()
    {
        if (!_started) { return; }
        _started = false;

        DisposeTimers();

        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("stop"); }
            catch (JSDisconnectedException) { }
        }
    }

    [JSInvokable]
    public void OnUserActivity()
    {
        // While the warning dialog is up, background activity is ignored —
        // the user must explicitly click "Stay Signed In" to extend the session.
        if (_warningOpen) { return; }
        ResetIdleTimers();
    }

    [JSInvokable]
    public Task OnRemoteLogout() => InvokeLogoutAsync(broadcast: false);

    private void ResetIdleTimers()
    {
        var idle = _options.IdleTimeout;
        var warningWindow = _options.WarningWindow;

        // Cap by the JWT expiry if it's sooner than the configured idle window.
        if (_jwtExpiresAtUtc is { } exp)
        {
            var untilExpiry = exp - DateTime.UtcNow;
            if (untilExpiry < idle) { idle = untilExpiry; }
        }

        if (idle <= TimeSpan.Zero)
        {
            _ = InvokeLogoutAsync(broadcast: true);
            return;
        }

        var warnAt = idle - warningWindow;
        if (warnAt < TimeSpan.Zero) { warnAt = TimeSpan.Zero; }

        _warningTimer?.Dispose();
        _logoutTimer?.Dispose();

        _warningTimer = new System.Threading.Timer(_ => _ = ShowWarningAsync(),
            null, warnAt, Timeout.InfiniteTimeSpan);
        _logoutTimer = new System.Threading.Timer(_ => _ = InvokeLogoutAsync(broadcast: true),
            null, idle, Timeout.InfiniteTimeSpan);
    }

    private async Task ShowWarningAsync()
    {
        if (_warningOpen) { return; }
        _warningOpen = true;
        try
        {
            var stay = await _dialog.OpenAsync<IdleWarningDialog>(
                "Are you still there?",
                new Dictionary<string, object?> { ["WarningSeconds"] = _options.WarningSeconds },
                new DialogOptions
                {
                    CloseDialogOnEsc = false,
                    CloseDialogOnOverlayClick = false,
                    ShowClose = false,
                    Width = "420px"
                });

            if (stay is true)
            {
                ResetIdleTimers();
            }
            else
            {
                await InvokeLogoutAsync(broadcast: true);
            }
        }
        finally
        {
            _warningOpen = false;
        }
    }

    private async Task ScheduleJwtExpiryAsync()
    {
        _jwtExpiresAtUtc = null;
        _jwtExpiryTimer?.Dispose();
        _jwtExpiryTimer = null;

        if (!_options.RespectJwtExpiry) { return; }

        var token = await _localStorage.GetItemAsStringAsync("accessToken");
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("eyJ", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            if (jwt.ValidTo == default) { return; }

            _jwtExpiresAtUtc = jwt.ValidTo;
            var untilExpiry = jwt.ValidTo - DateTime.UtcNow;
            if (untilExpiry <= TimeSpan.Zero)
            {
                _ = InvokeLogoutAsync(broadcast: true);
                return;
            }

            _jwtExpiryTimer = new System.Threading.Timer(_ => _ = InvokeLogoutAsync(broadcast: true),
                null, untilExpiry, Timeout.InfiniteTimeSpan);
        }
        catch
        {
            // Unparseable token — fall back to idle-only behavior.
        }
    }

    private async Task InvokeLogoutAsync(bool broadcast)
    {
        try
        {
            if (broadcast && _module is not null)
            {
                try { await _module.InvokeVoidAsync("broadcastLogout"); }
                catch (JSDisconnectedException) { }
            }

            await StopAsync();
            await _authService.LogoutAsync("timeout");
        }
        catch
        {
            // Background timer ticks must never throw out of the dispatcher.
        }
    }

    private void DisposeTimers()
    {
        _warningTimer?.Dispose(); _warningTimer = null;
        _logoutTimer?.Dispose(); _logoutTimer = null;
        _jwtExpiryTimer?.Dispose(); _jwtExpiryTimer = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _selfRef?.Dispose();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
