namespace ECTSystem.Web.Services;

/// <summary>
/// Represents the bearer token response returned by the ASP.NET Core Identity login endpoint.
/// Contains the access token, refresh token, and token metadata required for authenticated API requests.
/// </summary>
public class AuthResponse
{
    /// <summary>
    /// Gets or sets the token type (typically <c>"Bearer"</c>).
    /// </summary>
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JWT access token used in the <c>Authorization</c> header for authenticated API calls.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token lifetime in seconds from the time of issuance.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the refresh token used to obtain a new access token after expiration
    /// without requiring the user to re-authenticate.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
