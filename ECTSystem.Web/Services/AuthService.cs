using System.Net.Http.Json;
using Blazored.LocalStorage;
using ECTSystem.Web.Models;
using ECTSystem.Web.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ECTSystem.Web.Services;

/// <summary>
/// Defines the authentication contract for user login, registration, and logout operations.
/// Implementations are responsible for communicating with the ASP.NET Core Identity API endpoints.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user with the provided credentials. On success, stores JWT tokens
    /// in local storage and notifies the authentication state provider.
    /// </summary>
    /// <param name="email">The user's email address (used as the Identity username).</param>
    /// <param name="password">The user's password.</param>
    /// <returns>An <see cref="AuthResult"/> indicating whether the login succeeded, with an error message on failure.</returns>
    Task<AuthResult> LoginAsync(string email, string password);

    /// <summary>
    /// Registers a new user account via the ASP.NET Core Identity <c>/register</c> endpoint.
    /// Does not automatically log the user in upon successful registration.
    /// </summary>
    /// <param name="email">The email address for the new account.</param>
    /// <param name="password">The password for the new account. Must meet Identity password policy requirements.</param>
    /// <returns>An <see cref="AuthResult"/> indicating whether the registration succeeded, with an error message on failure.</returns>
    Task<AuthResult> RegisterAsync(string email, string password);

    /// <summary>
    /// Logs out the current user by removing JWT tokens from local storage,
    /// notifying the authentication state provider to revert to an unauthenticated state,
    /// and navigating to the login page with a full page reload to fully clear in-memory state.
    /// </summary>
    /// <param name="reason">Optional reason appended to the login URL as <c>?reason={reason}</c> (e.g. <c>"timeout"</c>).</param>
    Task LogoutAsync(string reason = null);
}

/// <summary>
/// Encapsulates the result of an authentication operation (login or registration).
/// </summary>
public class AuthResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the authentication operation completed successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the error message when <see cref="Succeeded"/> is <c>false</c>.
    /// Contains the server-returned error body, or a generic fallback message if the server response was empty.
    /// </summary>
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Implements <see cref="IAuthService"/> using ASP.NET Core Identity REST endpoints
/// (<c>/login</c> and <c>/register</c>) with JWT bearer token authentication.
/// Stores access and refresh tokens in <see cref="ILocalStorageService"/> and notifies
/// <see cref="JwtAuthStateProvider"/> of authentication state changes so the Blazor
/// <c>AuthorizeView</c> and <c>CascadingAuthenticationState</c> components update reactively.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Token storage trade-off:</strong> tokens live in browser <c>localStorage</c>, which is
/// readable by any script running on the page. A standalone Blazor WebAssembly app cannot use
/// <c>HttpOnly</c> cookies for bearer tokens without a same-origin server proxy, so the
/// mitigation strategy is the existing CSP headers configured in <c>Program.cs</c> plus strict
/// input sanitization on every page that renders user-supplied content. Do not loosen the CSP
/// to work around third-party scripts without re-evaluating this risk.
/// </para>
/// <para>
/// <strong>No automatic token refresh (yet):</strong> <see cref="AuthResponse.RefreshToken"/>
/// is persisted but no <c>DelegatingHandler</c> currently intercepts 401 responses to swap it
/// for a fresh access token. A 401 today forces the user back to the login page; a token
/// refresh handler is the tracked follow-up for this service.
/// </para>
/// </remarks>
public class AuthService : IAuthService
{
    /// <summary>
    /// The HTTP client configured with the API base address for sending authentication requests.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// The browser local storage service for persisting JWT access and refresh tokens across sessions.
    /// </summary>
    private readonly ILocalStorageService _localStorage;

    /// <summary>
    /// The custom authentication state provider that parses JWT claims and notifies Blazor of auth state changes.
    /// </summary>
    private readonly JwtAuthStateProvider _authStateProvider;

    /// <summary>
    /// The service that caches the current user's server-assigned identity.
    /// </summary>
    private readonly CurrentUserService _currentUserService;

    /// <summary>
    /// Logger for diagnostic events emitted by this service.
    /// </summary>
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// The navigation manager used to redirect to the login page after logout.
    /// </summary>
    private readonly NavigationManager _navigation;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with the API base address.</param>
    /// <param name="localStorage">The local storage service for persisting JWT tokens.</param>
    /// <param name="authStateProvider">The JWT authentication state provider for notifying auth state changes.</param>
    /// <param name="currentUserService">The service that caches the current user's identity.</param>
    /// <param name="logger">The logger for diagnostic events.</param>
    /// <param name="navigation">The navigation manager used to redirect to the login page on logout.</param>
    public AuthService(HttpClient httpClient, ILocalStorageService localStorage, JwtAuthStateProvider authStateProvider, CurrentUserService currentUserService, ILogger<AuthService> logger, NavigationManager navigation)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _currentUserService = currentUserService;
        _logger = logger;
        _navigation = navigation;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends a POST request to the <c>/login</c> endpoint with a <see cref="LoginRequest"/> payload.
    /// On success, extracts the <see cref="AuthResponse"/> containing the access and refresh tokens,
    /// persists them in local storage under keys <c>"accessToken"</c> and <c>"refreshToken"</c>,
    /// and notifies the authentication state provider to trigger UI updates.
    /// </remarks>
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new AuthResult { Succeeded = false, Error = string.IsNullOrWhiteSpace(body) ? "Login failed." : body };
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth is null)
        {
            return new AuthResult { Succeeded = false, Error = "Invalid response from server." };
        }

        await _localStorage.SetItemAsStringAsync("accessToken", auth.AccessToken);
        await _localStorage.SetItemAsStringAsync("refreshToken", auth.RefreshToken);

        _authStateProvider.NotifyAuthenticationStateChanged();

        return new AuthResult { Succeeded = true };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends a POST request to the <c>/register</c> endpoint with a <see cref="RegisterRequest"/> payload.
    /// Registration does not trigger an automatic login; the caller should invoke
    /// <see cref="LoginAsync"/> separately after successful registration.
    /// </remarks>
    public async Task<AuthResult> RegisterAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("register", new RegisterRequest
        {
            Email = email,
            Password = password
        });

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new AuthResult { Succeeded = false, Error = string.IsNullOrWhiteSpace(body) ? "Registration failed." : body };
        }

        return new AuthResult { Succeeded = true };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Removes both <c>"accessToken"</c> and <c>"refreshToken"</c> from local storage,
    /// notifies the authentication state provider to revert to an anonymous/unauthenticated state,
    /// and soft-navigates to <c>/login</c>. A soft navigation is used so the Blazor WebAssembly
    /// runtime is not re-bootstrapped (which would briefly show the boot loading indicator);
    /// the in-memory auth state is already invalidated via <see cref="JwtAuthStateProvider.NotifyAuthenticationStateChanged"/>
    /// and <see cref="CurrentUserService.Clear"/>.
    /// Centralising the redirect here ensures every logout entry point lands on the login page
    /// regardless of which route was active when logout was triggered.
    /// </remarks>
    public async Task LogoutAsync(string reason = null)
    {
        await _localStorage.RemoveItemAsync("accessToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        _currentUserService.Clear();
        _authStateProvider.NotifyAuthenticationStateChanged();

        var url = string.IsNullOrWhiteSpace(reason) ? "/login" : $"/login?reason={Uri.EscapeDataString(reason)}";
        _navigation.NavigateTo(url, forceLoad: false);
    }
}
