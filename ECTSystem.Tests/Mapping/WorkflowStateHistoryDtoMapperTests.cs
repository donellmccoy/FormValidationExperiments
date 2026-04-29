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
    /// Verifies that the supported DTO properties are mapped to the entity. EnteredDate
    /// and ExitDate are intentionally not on the DTO — they are stamped server-side
    /// via TimeProvider (§2.7 N1) and must remain at their entity defaults after mapping.
    /// </summary>
    [Fact]
    public void ToEntity_AllPropertiesSet_MapsAllProperties()
    {
        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = 10,
            WorkflowState = WorkflowState.UnitCommanderReview,
        };

        var entity = WorkflowStateHistoryDtoMapper.ToEntity(dto);

        Assert.Equal(10, entity.LineOfDutyCaseId);
        Assert.Equal(WorkflowState.UnitCommanderReview, entity.WorkflowState);
        Assert.Equal(default, entity.EnteredDate);
        Assert.Null(entity.ExitDate);
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
    /// Verifies that the mapper never copies a date onto the entity. Audit timestamps
    /// are server-authoritative; the mapper output must always have default EnteredDate
    /// and null ExitDate so the controller can stamp them via TimeProvider.
    /// </summary>
    [Fact]
    public void ToEntity_DoesNotMapAuditTimestamps()
    {
        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = 5,
            WorkflowState = WorkflowState.MedicalTechnicianReview,
        };

        var entity = WorkflowStateHistoryDtoMapper.ToEntity(dto);

        Assert.Equal(default, entity.EnteredDate);
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
