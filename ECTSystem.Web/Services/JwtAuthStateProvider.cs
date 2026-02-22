using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace ECTSystem.Web.Services;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;

    public JwtAuthStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsStringAsync("accessToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthenticated();
        }

        // JWT tokens start with "eyJ" — parse claims from the payload
        if (token.StartsWith("eyJ", StringComparison.Ordinal))
        {
            var principal = ParseClaimsFromJwt(token);
            if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true })
            {
                return Unauthenticated();
            }

            var exp = principal.FindFirst("exp")?.Value;
            if (exp is not null && long.TryParse(exp, out var expSeconds))
            {
                if (DateTimeOffset.FromUnixTimeSeconds(expSeconds) <= DateTimeOffset.UtcNow)
                {
                    return Unauthenticated();
                }
            }

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(principal.Claims, "jwt")));
        }

        // Opaque bearer token (ASP.NET Core Identity Data Protection token) —
        // validated server-side only. Treat as authenticated if present.
        var identity = new ClaimsIdentity("Bearer");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static AuthenticationState Unauthenticated() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static ClaimsPrincipal ParseClaimsFromJwt(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return new ClaimsPrincipal(new ClaimsIdentity(token.Claims, "jwt"));
        }
        catch
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
