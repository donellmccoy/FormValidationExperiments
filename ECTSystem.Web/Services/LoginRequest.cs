namespace ECTSystem.Web.Services;

/// <summary>
/// Represents a user login request sent to the ASP.NET Core Identity <c>/login</c> endpoint.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Gets or sets the user's email address used as the login identifier.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's plaintext password for authentication.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
