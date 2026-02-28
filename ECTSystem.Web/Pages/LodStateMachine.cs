using Stateless;
using Stateless.Graph;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.ViewModels;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ECTSystem.Shared.Models;

namespace ECTSystem.Web.Pages;

internal class LodStateMachine
{
    private readonly StateMachine<WorkflowState, LodTrigger> _sm;

    private readonly LineOfDutyCase _lineOfDutyCase;

    public WorkflowState State => _sm.State;

    public async Task<IEnumerable<LodTrigger>> GetPermittedTriggersAsync() => await _sm.GetPermittedTriggersAsync();

    public LodStateMachine(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;
        _sm = new StateMachine<WorkflowState, LodTrigger>(lineOfDutyCase.WorkflowState);

        _sm.OnTransitionedAsync(transition =>
        {
            Console.WriteLine($"Trigger: {transition.Trigger} | {transition.Source} -> {transition.Destination}");
            return Task.CompletedTask;
        });

        Configure();
    }

    /// <summary>
    /// save the state when the state machine changes states
    /// 
    /// </summary>
    private void Configure()
    {
        _sm.Configure(WorkflowState.MemberInformationEntry)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Member Information Entry state")))
            .Permit(LodTrigger.ForwardToMedicalTechnician, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Member Information Entry state")));

        _sm.Configure(WorkflowState.MedicalTechnicianReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Medical Technician Review state")))
            .Permit(LodTrigger.ForwardToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Medical Technician Review state")));

        _sm.Configure(WorkflowState.MedicalOfficerReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Medical Officer Review state")))
            .Permit(LodTrigger.ForwardToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Medical Officer Review state")));

        _sm.Configure(WorkflowState.UnitCommanderReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Unit Commander Review state")))
            .Permit(LodTrigger.ForwardToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Unit Commander Review state")));

        _sm.Configure(WorkflowState.WingJudgeAdvocateReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Wing Judge Advocate Review state")))
            .Permit(LodTrigger.ForwardToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Wing Judge Advocate Review state")));

        _sm.Configure(WorkflowState.WingCommanderReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Wing Commander Review state")))
            .Permit(LodTrigger.ForwardToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Wing Commander Review state")));

        _sm.Configure(WorkflowState.AppointingAuthorityReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Appointing Authority Review state")))
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Appointing Authority Review state")));

        _sm.Configure(WorkflowState.BoardMedicalTechnicianReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Board Medical Technician Review state")))
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Board Medical Technician Review state")));

        _sm.Configure(WorkflowState.BoardMedicalOfficerReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Board Medical Officer Review state")))
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Board Medical Officer Review state")));

        _sm.Configure(WorkflowState.BoardLegalReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Board Legal Review state")))
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Board Legal Review state")));

        _sm.Configure(WorkflowState.BoardAdministratorReview)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Board Administrator Review state")))
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.Complete, WorkflowState.Completed)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Board Administrator Review state")));

        _sm.Configure(WorkflowState.Completed)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Completed state")))
            .Ignore(LodTrigger.Cancel)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Completed state")));

        _sm.Configure(WorkflowState.Cancelled)
            .OnEntryAsync(() => Task.Run(() => Console.WriteLine("Entered Cancelled state")))
            .Ignore(LodTrigger.Cancel)
            .OnExitAsync(() => Task.Run(() => Console.WriteLine("Exiting Cancelled state")));
    }

    public bool CanFire(LodTrigger trigger) => _sm.CanFire(trigger);

    public void Fire(LodTrigger trigger) => _sm.Fire(trigger);

    public async Task FireAsync(LodTrigger trigger) => await _sm.FireAsync(trigger);

    public string ToMermaidGraph() => MermaidGraph.Format(_sm.GetInfo());
}
