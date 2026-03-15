using System.Net.Http.Json;
using Blazored.LocalStorage;
using ECTSystem.Web.Providers;

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
    /// Logs out the current user by removing JWT tokens from local storage and
    /// notifying the authentication state provider to revert to an unauthenticated state.
    /// </summary>
    Task LogoutAsync();
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
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with the API base address.</param>
    /// <param name="localStorage">The local storage service for persisting JWT tokens.</param>
    /// <param name="authStateProvider">The JWT authentication state provider for notifying auth state changes.</param>
    public AuthService(HttpClient httpClient, ILocalStorageService localStorage, JwtAuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
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
    /// then notifies the authentication state provider to revert to an anonymous/unauthenticated state.
    /// </remarks>
    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync("accessToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        _authStateProvider.NotifyAuthenticationStateChanged();
    }
}
