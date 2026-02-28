using Stateless;
using Stateless.Graph;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Web.Pages;

internal class LodStateMachine
{
    private readonly StateMachine<WorkflowState, LodTrigger> _sm;

    public WorkflowState State => _sm.State;

    public async Task<IEnumerable<LodTrigger>> GetPermittedTriggersAsync() => await _sm.GetPermittedTriggersAsync();

    public LodStateMachine(WorkflowState initialState)
    {
        _sm = new StateMachine<WorkflowState, LodTrigger>(initialState);
        
        Configure();
    }

    private void Configure()
    {
        _sm.Configure(WorkflowState.MemberInformationEntry)
            .Permit(LodTrigger.ForwardToMedicalTechnician, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.MemberInformationEntry)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ForwardToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ForwardToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ForwardToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ForwardToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardTechnicianReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.BoardTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.BoardMedicalReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.Complete, WorkflowState.Completed)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        _sm.Configure(WorkflowState.Completed)
            .Ignore(LodTrigger.Cancel);

        _sm.Configure(WorkflowState.Cancelled)
            .Ignore(LodTrigger.Cancel);
    }

    public bool CanFire(LodTrigger trigger) => _sm.CanFire(trigger);

    public void Fire(LodTrigger trigger) => _sm.Fire(trigger);

    public async Task FireAsync(LodTrigger trigger) => await _sm.FireAsync(trigger);

    public string ToMermaidGraph() => MermaidGraph.Format(_sm.GetInfo());
}
