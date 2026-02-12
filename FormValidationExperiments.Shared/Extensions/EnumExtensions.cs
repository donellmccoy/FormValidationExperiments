using System.Text.RegularExpressions;

namespace FormValidationExperiments.Shared.Extensions;

/// <summary>
/// Extension methods for <see cref="Enum"/> types.
/// </summary>
public static partial class EnumExtensions
{
    /// <summary>
    /// Formats an enum value as a display-friendly string by inserting spaces
    /// before uppercase letters (e.g., <c>NotInLineOfDuty</c> → <c>"Not In Line Of Duty"</c>).
    /// </summary>
    public static string ToDisplayString(this Enum value)
    {
        return PascalCaseRegex().Replace(value.ToString(), " $1");
    }

    [GeneratedRegex(@"(\B[A-Z])")]
    private static partial Regex PascalCaseRegex();
}
