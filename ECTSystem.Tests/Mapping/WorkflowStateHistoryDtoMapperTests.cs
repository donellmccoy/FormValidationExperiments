using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Mapping;

/// <summary>
/// Unit tests for <see cref="WorkflowStateHistoryDtoMapper"/>, which maps
/// <see cref="CreateWorkflowStateHistoryDto"/> to <see cref="Shared.Models.WorkflowStateHistory"/>.
/// </summary>
public class WorkflowStateHistoryDtoMapperTests
{
    /// <summary>
    /// Verifies that all properties are correctly mapped from the DTO to the entity.
    /// </summary>
    [Fact]
    public void ToEntity_AllPropertiesSet_MapsAllProperties()
    {
        var startDate = new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 3, 5, 17, 0, 0, DateTimeKind.Utc);
        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = 10,
            WorkflowState = WorkflowState.UnitCommanderReview,
            EnteredDate = startDate,
            ExitDate = endDate
        };

        var entity = WorkflowStateHistoryDtoMapper.ToEntity(dto);

        Assert.Equal(10, entity.LineOfDutyCaseId);
        Assert.Equal(WorkflowState.UnitCommanderReview, entity.WorkflowState);
        Assert.Equal(startDate, entity.EnteredDate);
        Assert.Equal(endDate, entity.ExitDate);
    }

    /// <summary>
    /// Verifies that default DTO values produce an entity with default enum values and null dates.
    /// </summary>
    [Fact]
    public void ToEntity_DefaultDto_MapsDefaults()
    {
        var dto = new CreateWorkflowStateHistoryDto();

        var entity = WorkflowStateHistoryDtoMapper.ToEntity(dto);

        Assert.Equal(0, entity.LineOfDutyCaseId);
        Assert.Equal(default, entity.WorkflowState);
        Assert.Equal(default, entity.EnteredDate);
        Assert.Null(entity.ExitDate);
    }

    /// <summary>
    /// Verifies that null EndDate is preserved when mapping an in-progress workflow entry.
    /// </summary>
    [Fact]
    public void ToEntity_NullEndDate_MapsNull()
    {
        var startDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = 5,
            WorkflowState = WorkflowState.MedicalTechnicianReview,
            EnteredDate = startDate,
            ExitDate = null
        };

        var entity = WorkflowStateHistoryDtoMapper.ToEntity(dto);

        Assert.Equal(startDate, entity.EnteredDate);
        Assert.Null(entity.ExitDate);
    }

    /// <summary>
    /// Verifies mapping works for each terminal workflow state.
    /// </summary>
    [Theory]
    [InlineData(WorkflowState.Completed)]
    [InlineData(WorkflowState.Cancelled)]
    [InlineData(WorkflowState.Closed)]
    public void ToEntity_TerminalStates_MapsCorrectly(WorkflowState terminalState)
    {
        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = 1,
            WorkflowState = terminalState,
        };

        var entity = WorkflowStateHistoryDtoMapper.ToEntity(dto);

        Assert.Equal(terminalState, entity.WorkflowState);
    }
}
