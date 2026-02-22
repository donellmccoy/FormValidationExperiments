using System.Net.Http.Json;
using Blazored.LocalStorage;

namespace ECTSystem.Web.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RegisterAsync(string email, string password);
    Task LogoutAsync();
}

public class AuthResult
{
    public bool Succeeded { get; set; }
    public string Error { get; set; } = string.Empty;
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly JwtAuthStateProvider _authStateProvider;

    public AuthService(HttpClient httpClient, ILocalStorageService localStorage, JwtAuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

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

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync("accessToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        _authStateProvider.NotifyAuthenticationStateChanged();
    }
}
