using Humanizer;

namespace ECTSystem.Shared.Extensions;

/// <summary>
/// Extension methods for <see cref="Enum"/> types.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Formats an enum value as a display-friendly string by inserting spaces
    /// before uppercase letters (e.g., <c>NotInLineOfDuty</c> → <c>"Not In Line Of Duty"</c>).
    /// </summary>
    public static string ToDisplayString(this Enum value)
    {
        return value.ToString().Humanize(LetterCasing.Title);
    }
}

//public static class LineOfDutyExtensions
//{
//    public static bool Has
//}
