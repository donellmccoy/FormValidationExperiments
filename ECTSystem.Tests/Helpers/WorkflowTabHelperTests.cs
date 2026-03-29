using ECTSystem.Shared.Enums;
using ECTSystem.Web.Helpers;
using Xunit;

namespace ECTSystem.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="WorkflowTabHelper"/>, which provides the mapping between
/// <see cref="WorkflowState"/> values and wizard tab indices for the LOD determination workflow.
/// </summary>
public class WorkflowTabHelperTests
{
    // --- GetTabIndexForState: workflow states ---

    /// <summary>
    /// Verifies that each workflow state maps to its expected zero-based tab index.
    /// The order matches the AF Form 348 workflow progression.
    /// </summary>
    [Theory]
    [InlineData(WorkflowState.MemberInformationEntry, 0)]
    [InlineData(WorkflowState.MedicalTechnicianReview, 1)]
    [InlineData(WorkflowState.MedicalOfficerReview, 2)]
    [InlineData(WorkflowState.UnitCommanderReview, 3)]
    [InlineData(WorkflowState.WingJudgeAdvocateReview, 4)]
    [InlineData(WorkflowState.AppointingAuthorityReview, 5)]
    [InlineData(WorkflowState.WingCommanderReview, 6)]
    [InlineData(WorkflowState.BoardMedicalTechnicianReview, 7)]
    [InlineData(WorkflowState.BoardMedicalOfficerReview, 8)]
    [InlineData(WorkflowState.BoardLegalReview, 9)]
    [InlineData(WorkflowState.BoardAdministratorReview, 10)]
    public void GetTabIndexForState_WorkflowState_ReturnsExpectedIndex(WorkflowState state, int expectedIndex)
    {
        var result = WorkflowTabHelper.GetTabIndexForState(state);

        Assert.Equal(expectedIndex, result);
    }

    /// <summary>
    /// Verifies that Completed maps to the last workflow tab (index 10).
    /// </summary>
    [Fact]
    public void GetTabIndexForState_Completed_ReturnsLastTabIndex()
    {
        var result = WorkflowTabHelper.GetTabIndexForState(WorkflowState.Completed);

        Assert.Equal(10, result);
    }

    /// <summary>
    /// Verifies that Draft maps to tab index 0.
    /// </summary>
    [Fact]
    public void GetTabIndexForState_Draft_ReturnsZero()
    {
        var result = WorkflowTabHelper.GetTabIndexForState(WorkflowState.Draft);

        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies that Cancelled (not in the tab map) falls through to the default case (index 0).
    /// </summary>
    [Fact]
    public void GetTabIndexForState_Cancelled_ReturnsZero()
    {
        var result = WorkflowTabHelper.GetTabIndexForState(WorkflowState.Cancelled);

        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies that Closed (not in the tab map) falls through to the default case (index 0).
    /// </summary>
    [Fact]
    public void GetTabIndexForState_Closed_ReturnsZero()
    {
        var result = WorkflowTabHelper.GetTabIndexForState(WorkflowState.Closed);

        Assert.Equal(0, result);
    }

    // --- IsTabDisabled ---

    /// <summary>
    /// Verifies that a tab at the current workflow state is not disabled.
    /// </summary>
    [Fact]
    public void IsTabDisabled_TabAtCurrentState_ReturnsFalse()
    {
        // UnitCommanderReview = index 3
        var result = WorkflowTabHelper.IsTabDisabled(3, WorkflowState.UnitCommanderReview);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that a tab before the current workflow state is not disabled.
    /// </summary>
    [Fact]
    public void IsTabDisabled_TabBeforeCurrentState_ReturnsFalse()
    {
        // Tab 0 (MemberInfo) when current is UnitCommanderReview (index 3)
        var result = WorkflowTabHelper.IsTabDisabled(0, WorkflowState.UnitCommanderReview);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that a tab after the current workflow state is disabled.
    /// </summary>
    [Fact]
    public void IsTabDisabled_TabAfterCurrentState_ReturnsTrue()
    {
        // Tab 5 (AppointingAuthority) when current is UnitCommanderReview (index 3)
        var result = WorkflowTabHelper.IsTabDisabled(5, WorkflowState.UnitCommanderReview);

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that non-workflow tabs (beyond the 11-tab map) are always enabled.
    /// These represent tabs like Documents or Timeline that aren't part of the workflow sequence.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(20)]
    public void IsTabDisabled_TabBeyondWorkflowRange_ReturnsFalse(int tabIndex)
    {
        // Even with Draft (earliest state), non-workflow tabs should be enabled
        var result = WorkflowTabHelper.IsTabDisabled(tabIndex, WorkflowState.Draft);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that when the case is Completed, all workflow tabs are enabled
    /// since Completed maps to the last tab index.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void IsTabDisabled_CompletedState_AllWorkflowTabsEnabled(int tabIndex)
    {
        var result = WorkflowTabHelper.IsTabDisabled(tabIndex, WorkflowState.Completed);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that for Draft state, all workflow tabs except the first are disabled.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void IsTabDisabled_DraftState_AllTabsAfterFirstDisabled(int tabIndex)
    {
        var result = WorkflowTabHelper.IsTabDisabled(tabIndex, WorkflowState.Draft);

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that the first tab is never disabled regardless of current state.
    /// </summary>
    [Theory]
    [InlineData(WorkflowState.Draft)]
    [InlineData(WorkflowState.MemberInformationEntry)]
    [InlineData(WorkflowState.Completed)]
    public void IsTabDisabled_FirstTab_AlwaysEnabled(WorkflowState state)
    {
        var result = WorkflowTabHelper.IsTabDisabled(0, state);

        Assert.False(result);
    }
}
