namespace ECTSystem.Web.Services;

/// <summary>
/// Represents a new user registration request sent to the ASP.NET Core Identity <c>/register</c> endpoint.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Gets or sets the email address to register as a new account identifier.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plaintext password for the new account.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
