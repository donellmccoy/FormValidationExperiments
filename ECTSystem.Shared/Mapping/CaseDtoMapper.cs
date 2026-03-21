using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateCaseDto"/> to <see cref="LineOfDutyCase"/>.
/// </summary>
public static class CaseDtoMapper
{
    public static LineOfDutyCase ToEntity(CreateCaseDto dto)
    {
        return new LineOfDutyCase
        {
            MemberId = dto.MemberId,
            ProcessType = dto.ProcessType,
            Component = dto.Component,
            MemberName = dto.MemberName,
            MemberRank = dto.MemberRank,
            ServiceNumber = dto.ServiceNumber,
            MemberDateOfBirth = dto.MemberDateOfBirth,
            Unit = dto.Unit,
            FromLine = dto.FromLine,
            IncidentType = dto.IncidentType,
            IncidentDate = dto.IncidentDate,
            IncidentDescription = dto.IncidentDescription,
            IncidentDutyStatus = dto.IncidentDutyStatus,

            MemberOrdersStartTime = dto.MemberOrdersStartTime,
            MemberOrdersEndDate = dto.MemberOrdersEndDate,
            MemberOrdersEndTime = dto.MemberOrdersEndTime,

            IsMTF = dto.IsMTF,
            IsRMU = dto.IsRMU,
            IsGMU = dto.IsGMU,
            IsDeployedLocation = dto.IsDeployedLocation,
            IsUSAFA = dto.IsUSAFA,
            IsAFROTC = dto.IsAFROTC,

            IsMilitaryFacility = dto.IsMilitaryFacility,
            TreatmentFacilityName = dto.TreatmentFacilityName,
            TreatmentDateTime = dto.TreatmentDateTime,
            ClinicalDiagnosis = dto.ClinicalDiagnosis,
            MedicalFindings = dto.MedicalFindings,
            WasUnderInfluence = dto.WasUnderInfluence,
            SubstanceType = dto.SubstanceType,
            WasMentallyResponsible = dto.WasMentallyResponsible,
            PsychiatricEvalCompleted = dto.PsychiatricEvalCompleted,
            PsychiatricEvalDate = dto.PsychiatricEvalDate,
            PsychiatricEvalResults = dto.PsychiatricEvalResults,
            OtherRelevantConditions = dto.OtherRelevantConditions,
            OtherTestsDone = dto.OtherTestsDone,
            OtherTestDate = dto.OtherTestDate,
            OtherTestResults = dto.OtherTestResults,
            IsServiceAggravated = dto.IsServiceAggravated,
            IsPotentiallyUnfitting = dto.IsPotentiallyUnfitting,
            IsAtDeployedLocation = dto.IsAtDeployedLocation,
            RequiresArcBoard = dto.RequiresArcBoard,
            MedicalRecommendation = dto.MedicalRecommendation,

            MemberStatementReviewed = dto.MemberStatementReviewed,
            MedicalRecordsReviewed = dto.MedicalRecordsReviewed,
            WitnessStatementsReviewed = dto.WitnessStatementsReviewed,
            PoliceReportsReviewed = dto.PoliceReportsReviewed,
            CommanderReportReviewed = dto.CommanderReportReviewed,
            OsiReportsReviewed = dto.OsiReportsReviewed,
            MilitaryPoliceReportsReviewed = dto.MilitaryPoliceReportsReviewed,
            OtherSourcesReviewed = dto.OtherSourcesReviewed,
            OtherSourcesDescription = dto.OtherSourcesDescription,
            MisconductExplanation = dto.MisconductExplanation,
            CommanderToLine = dto.CommanderToLine,
            CommanderFromLine = dto.CommanderFromLine,

            WasPresentForDuty = dto.WasPresentForDuty,
            WasOnDuty = dto.WasOnDuty,
            WasOnIDT = dto.WasOnIDT,
            WasAbsentWithLeave = dto.WasAbsentWithLeave,
            WasAbsentWithoutLeave = dto.WasAbsentWithoutLeave,
            AbsentWithoutLeaveDate1 = dto.AbsentWithoutLeaveDate1,
            AbsentWithoutLeaveTime1 = dto.AbsentWithoutLeaveTime1,
            AbsentWithoutLeaveDate2 = dto.AbsentWithoutLeaveDate2,
            AbsentWithoutLeaveTime2 = dto.AbsentWithoutLeaveTime2,

            WitnessNameAddress1 = dto.WitnessNameAddress1,
            WitnessNameAddress2 = dto.WitnessNameAddress2,
            WitnessNameAddress3 = dto.WitnessNameAddress3,
            WitnessNameAddress4 = dto.WitnessNameAddress4,
            WitnessNameAddress5 = dto.WitnessNameAddress5,

            InitiationDate = dto.InitiationDate,
            CompletionDate = dto.CompletionDate,
            TotalTimelineDays = dto.TotalTimelineDays,
            IsInterimLOD = dto.IsInterimLOD,
            InterimLODExpiration = dto.InterimLODExpiration,

            FinalFinding = dto.FinalFinding,
            ProximateCause = dto.ProximateCause,
            IsPriorServiceCondition = dto.IsPriorServiceCondition,
            PSCDocumentation = dto.PSCDocumentation,
            EightYearRuleApplies = dto.EightYearRuleApplies,
            YearsOfService = dto.YearsOfService,

            ProviderNameRank = dto.ProviderNameRank,
            ProviderDate = dto.ProviderDate,
            ProviderSignature = dto.ProviderSignature,

            CommanderNameRank = dto.CommanderNameRank,
            CommanderDate = dto.CommanderDate,
            CommanderSignature = dto.CommanderSignature,

            SjaNameRank = dto.SjaNameRank,
            SjaDate = dto.SjaDate,
            SjaConcurs = dto.SjaConcurs,

            WingCcSignature = dto.WingCcSignature,
            AppointingAuthorityNameRank = dto.AppointingAuthorityNameRank,
            AppointingAuthorityDate = dto.AppointingAuthorityDate,
            AppointingAuthoritySignature = dto.AppointingAuthoritySignature,

            MedicalReviewText = dto.MedicalReviewText,
            MedicalReviewerNameRank = dto.MedicalReviewerNameRank,
            MedicalReviewDate = dto.MedicalReviewDate,
            MedicalReviewerSignature = dto.MedicalReviewerSignature,
            LegalReviewText = dto.LegalReviewText,
            LegalReviewerNameRank = dto.LegalReviewerNameRank,
            LegalReviewDate = dto.LegalReviewDate,
            LegalReviewerSignature = dto.LegalReviewerSignature,
            LodBoardChairNameRank = dto.LodBoardChairNameRank,
            LodBoardChairDate = dto.LodBoardChairDate,
            LodBoardChairSignature = dto.LodBoardChairSignature,
            BoardFinding = dto.BoardFinding,
            BoardReferForFormal = dto.BoardReferForFormal,

            ApprovingAuthorityNameRank = dto.ApprovingAuthorityNameRank,
            ApprovingAuthorityDate = dto.ApprovingAuthorityDate,
            ApprovingAuthoritySignature = dto.ApprovingAuthoritySignature,
            ApprovingFinding = dto.ApprovingFinding,
            ApprovingReferForFormal = dto.ApprovingReferForFormal,

            NotifiedMedicalUnitTimely = dto.NotifiedMedicalUnitTimely,
            SubmittedMedicalDocumentsTimely = dto.SubmittedMedicalDocumentsTimely,

            IsSexualAssaultCase = dto.IsSexualAssaultCase,
            RestrictedReporting = dto.RestrictedReporting,
            SARCCoordination = dto.SARCCoordination,

            ToxicologyReport = dto.ToxicologyReport,
            MemberChoseMEDCON = dto.MemberChoseMEDCON,
            PointOfContact = dto.PointOfContact,
        };
    }
}
