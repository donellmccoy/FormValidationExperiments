using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using Xunit;

namespace ECTSystem.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="EnumExtensions.ToDisplayString"/> which converts enum values
/// to human-readable display strings using Humanizer's <c>Humanize(LetterCasing.Title)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="EnumExtensions.ToDisplayString"/> method is used throughout the Blazor UI
/// to format enum values for dropdown labels, form field displays, and status indicators.
/// These tests verify correct title-cased output for multi-word, single-word, abbreviation,
/// and various domain-specific enum types.
/// </para>
/// </remarks>
public class EnumExtensionsTests
{
    /// <summary>
    /// Verifies that multi-word PascalCase enum values are humanized with spaces and title casing.
    /// Uses <see cref="CommanderRecommendation"/> values which have complex multi-word names.
    /// </summary>
    /// <param name="value">The enum value to format.</param>
    /// <param name="expected">The expected display string.</param>
    [Theory]
    [InlineData(CommanderRecommendation.InLineOfDuty, "In Line of Duty")]
    [InlineData(CommanderRecommendation.NotInLineOfDutyDueToMisconduct, "Not in Line of Duty Due to Misconduct")]
    [InlineData(CommanderRecommendation.NotInLineOfDutyNotDueToMisconduct, "Not in Line of Duty Not Due to Misconduct")]
    [InlineData(CommanderRecommendation.ReferToFormalInvestigation, "Refer to Formal Investigation")]
    public void ToDisplayString_CommanderRecommendation_ReturnsExpectedText(
        CommanderRecommendation value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    /// <summary>
    /// Verifies that single-word enum values are returned as-is with title casing.
    /// Uses <see cref="IncidentType"/> values which include single-word entries.
    /// </summary>
    /// <param name="value">The enum value to format.</param>
    /// <param name="expected">The expected display string.</param>
    [Theory]
    [InlineData(IncidentType.Injury, "Injury")]
    [InlineData(IncidentType.Illness, "Illness")]
    [InlineData(IncidentType.Disease, "Disease")]
    [InlineData(IncidentType.Death, "Death")]
    [InlineData(IncidentType.SexualAssault, "Sexual Assault")]
    public void ToDisplayString_IncidentType_ReturnsExpectedText(
        IncidentType value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    /// <summary>
    /// Verifies correct humanization for <see cref="DutyStatus"/> values which contain
    /// numeric digits and mixed casing patterns (e.g., <c>Title10ActiveDuty</c>).
    /// </summary>
    /// <param name="value">The enum value to format.</param>
    /// <param name="expected">The expected display string.</param>
    [Theory]
    [InlineData(DutyStatus.Title10ActiveDuty, "Title 10 Active Duty")]
    [InlineData(DutyStatus.Title32ActiveDuty, "Title 32 Active Duty")]
    [InlineData(DutyStatus.InactiveDutyTraining, "Inactive Duty Training")]
    [InlineData(DutyStatus.TravelToFromDuty, "Travel to From Duty")]
    [InlineData(DutyStatus.NotInDutyStatus, "Not in Duty Status")]
    public void ToDisplayString_DutyStatus_ReturnsExpectedText(
        DutyStatus value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    /// <summary>
    /// Verifies correct humanization for <see cref="ServiceComponent"/> values which
    /// have compound words (e.g., <c>RegularAirForce</c>).
    /// </summary>
    /// <param name="value">The enum value to format.</param>
    /// <param name="expected">The expected display string.</param>
    [Theory]
    [InlineData(ServiceComponent.RegularAirForce, "Regular Air Force")]
    [InlineData(ServiceComponent.UnitedStatesSpaceForce, "United States Space Force")]
    [InlineData(ServiceComponent.AirForceReserve, "Air Force Reserve")]
    [InlineData(ServiceComponent.AirNationalGuard, "Air National Guard")]
    public void ToDisplayString_ServiceComponent_ReturnsExpectedText(
        ServiceComponent value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    /// <summary>
    /// Verifies correct humanization for <see cref="WorkflowStepStatus"/> enum values
    /// which are used in workflow step tracking.
    /// </summary>
    /// <param name="value">The enum value to format.</param>
    /// <param name="expected">The expected display string.</param>
    [Theory]
    [InlineData(WorkflowStepStatus.Completed, "Completed")]
    [InlineData(WorkflowStepStatus.InProgress, "In Progress")]
    [InlineData(WorkflowStepStatus.Pending, "Pending")]
    [InlineData(WorkflowStepStatus.OnHold, "On Hold")]
    public void ToDisplayString_WorkflowStepStatus_ReturnsExpectedText(
        WorkflowStepStatus value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }
}
