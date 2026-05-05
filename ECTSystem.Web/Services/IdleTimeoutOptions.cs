namespace ECTSystem.Web.Services;

/// <summary>
/// Configuration for <see cref="IdleTimeoutService"/>. Bound from the
/// <c>IdleTimeout</c> section of <c>wwwroot/appsettings.json</c>.
/// </summary>
public sealed class IdleTimeoutOptions
{
    public const string SectionName = "IdleTimeout";

    /// <summary>Minutes of inactivity before the user is signed out.</summary>
    public int IdleTimeoutMinutes { get; set; } = 15;

    /// <summary>Seconds before logout to display the "are you still there?" warning dialog.</summary>
    public int WarningSeconds { get; set; } = 60;

    /// <summary>Throttle window for activity pings from the browser (seconds).</summary>
    public int ActivityThrottleSeconds { get; set; } = 2;

    /// <summary>When true, schedule a hard logout at the JWT <c>exp</c> claim if it falls inside the idle window.</summary>
    public bool RespectJwtExpiry { get; set; } = true;

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(IdleTimeoutMinutes);
    public TimeSpan WarningWindow => TimeSpan.FromSeconds(WarningSeconds);
    public TimeSpan ActivityThrottle => TimeSpan.FromSeconds(ActivityThrottleSeconds);
}
