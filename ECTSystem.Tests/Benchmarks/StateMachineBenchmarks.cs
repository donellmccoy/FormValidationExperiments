using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using ECTSystem.Web.StateMachines;
using ECTSystem.Web.ViewModels;
using Moq;

namespace ECTSystem.Tests.Benchmarks;

/// <summary>
/// Microbenchmarks for the <see cref="LineOfDutyStateMachine"/> workflow state machine.
/// Measures transition performance and permitted-trigger resolution.
/// Run from command line: dotnet run -c Release --project ECTSystem.Tests -- --filter *StateMachineBenchmarks*
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class StateMachineBenchmarks
{
    private Mock<IWorkflowHistoryService> _mockHistoryService;

    [GlobalSetup]
    public void Setup()
    {
        _mockHistoryService = new Mock<IWorkflowHistoryService>();
        _mockHistoryService
            .Setup(s => s.AddHistoryEntryAsync(It.IsAny<WorkflowStateHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowStateHistory entry, CancellationToken _) => new WorkflowStateHistory
            {
                Id = Random.Shared.Next(1, 10000),
                LineOfDutyCaseId = entry.LineOfDutyCaseId,
                WorkflowState = entry.WorkflowState,
                EnteredDate = DateTime.UtcNow
            });

        _mockHistoryService
            .Setup(s => s.UpdateHistoryEndDateAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int entryId, DateTime endDate, CancellationToken _) => new WorkflowStateHistory
            {
                Id = entryId,
                ExitDate = endDate
            });
    }

    [Benchmark(Baseline = true)]
    public object CreateStateMachine_Draft()
    {
        var lodCase = CreateCaseInState(WorkflowState.Draft);
        return new LineOfDutyStateMachine(lodCase, _mockHistoryService.Object);
    }

    [Benchmark]
    public async Task<object> SingleForwardTransition()
    {
        var lodCase = CreateCaseInState(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _mockHistoryService.Object);
        return await sm.FireAsync(WorkflowTrigger.ForwardToMemberInformationEntry);
    }

    [Benchmark]
    public async Task<List<WorkflowTrigger>> GetPermittedTriggers_UnitCommander()
    {
        var lodCase = CreateCaseInState(WorkflowState.UnitCommanderReview);
        var sm = new LineOfDutyStateMachine(lodCase, _mockHistoryService.Object);
        var triggers = await sm.GetPermittedTriggersAsync();
        return triggers.ToList();
    }

    [Benchmark]
    public async Task FullForwardPath_DraftToCompleted()
    {
        var lodCase = CreateCaseInState(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _mockHistoryService.Object);

        // Walk the entire forward path
        WorkflowTrigger[] forwardTriggers =
        [
            WorkflowTrigger.ForwardToMemberInformationEntry,    // Draft → MemberInformationEntry
            WorkflowTrigger.ForwardToMedicalTechnician,         // → MedicalTechnicianReview
            WorkflowTrigger.ForwardToMedicalOfficerReview,      // → MedicalOfficerReview
            WorkflowTrigger.ForwardToUnitCommanderReview,       // → UnitCommanderReview
            WorkflowTrigger.ForwardToWingJudgeAdvocateReview,   // → WingJudgeAdvocateReview
            WorkflowTrigger.ForwardToAppointingAuthorityReview, // → AppointingAuthorityReview
            WorkflowTrigger.ForwardToWingCommanderReview,       // → WingCommanderReview
            WorkflowTrigger.ForwardToBoardTechnicianReview,     // → BoardMedicalTechnicianReview
            WorkflowTrigger.ForwardToBoardMedicalReview,        // → BoardMedicalOfficerReview
            WorkflowTrigger.ForwardToBoardLegalReview,          // → BoardLegalReview
            WorkflowTrigger.ForwardToBoardAdministratorReview,  // → BoardAdministratorReview
            WorkflowTrigger.Complete,                           // → Completed
        ];

        foreach (var trigger in forwardTriggers)
        {
            await sm.FireAsync(trigger);
        }
    }

    private static LineOfDutyCase CreateCaseInState(WorkflowState state)
    {
        return new LineOfDutyCase
        {
            Id = 1,
            CaseId = "20250315-001",
            MemberName = "Doe, John A.",
            MemberRank = "TSgt",
            Component = ServiceComponent.RegularAirForce,
            IncidentType = IncidentType.Injury,
            ProcessType = ProcessType.Informal,
            WorkflowStateHistories = new List<WorkflowStateHistory>
            {
                new()
                {
                    Id = 1,
                    WorkflowState = state,
                    EnteredDate = DateTime.UtcNow,
                    ExitDate = null
                }
            }
        };
    }
}
