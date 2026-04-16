using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

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
        };
    }

    public static void ApplyUpdate(UpdateCaseDto dto, LineOfDutyCase entity)
    {
        // Basic Case Information
        entity.ProcessType = dto.ProcessType;
        entity.Component = dto.Component;
        entity.MemberName = dto.MemberName;
        entity.MemberRank = dto.MemberRank;
        entity.ServiceNumber = dto.ServiceNumber;
        entity.MemberDateOfBirth = dto.MemberDateOfBirth;
        entity.Unit = dto.Unit;
        entity.FromLine = dto.FromLine;
        entity.IncidentType = dto.IncidentType;
        entity.IncidentDate = dto.IncidentDate;
        entity.IncidentDescription = dto.IncidentDescription;
        entity.IncidentDutyStatus = dto.IncidentDutyStatus;

        // Part I: Orders / Duty Period
        entity.MemberOrdersStartTime = dto.MemberOrdersStartTime;
        entity.MemberOrdersEndDate = dto.MemberOrdersEndDate;
        entity.MemberOrdersEndTime = dto.MemberOrdersEndTime;

        // Part I Item 2: Type of Medical Unit Reporting
        entity.IsMTF = dto.IsMTF;
        entity.IsRMU = dto.IsRMU;
        entity.IsGMU = dto.IsGMU;
        entity.IsDeployedLocation = dto.IsDeployedLocation;

        // Part I Item 8
        entity.IsUSAFA = dto.IsUSAFA;
        entity.IsAFROTC = dto.IsAFROTC;

        // Medical Assessment
        entity.IsMilitaryFacility = dto.IsMilitaryFacility;
        entity.TreatmentFacilityName = dto.TreatmentFacilityName;
        entity.TreatmentDateTime = dto.TreatmentDateTime;
        entity.ClinicalDiagnosis = dto.ClinicalDiagnosis;
        entity.MedicalFindings = dto.MedicalFindings;
        entity.WasUnderInfluence = dto.WasUnderInfluence;
        entity.SubstanceType = dto.SubstanceType;
        entity.WasMentallyResponsible = dto.WasMentallyResponsible;
        entity.PsychiatricEvalCompleted = dto.PsychiatricEvalCompleted;
        entity.PsychiatricEvalDate = dto.PsychiatricEvalDate;
        entity.PsychiatricEvalResults = dto.PsychiatricEvalResults;
        entity.OtherRelevantConditions = dto.OtherRelevantConditions;
        entity.OtherTestsDone = dto.OtherTestsDone;
        entity.OtherTestDate = dto.OtherTestDate;
        entity.OtherTestResults = dto.OtherTestResults;
        entity.IsServiceAggravated = dto.IsServiceAggravated;
        entity.IsPotentiallyUnfitting = dto.IsPotentiallyUnfitting;
        entity.IsAtDeployedLocation = dto.IsAtDeployedLocation;
        entity.RequiresArcBoard = dto.RequiresArcBoard;
        entity.MedicalRecommendation = dto.MedicalRecommendation;

        // Commander Review — Sources of Information
        entity.MemberStatementReviewed = dto.MemberStatementReviewed;
        entity.MedicalRecordsReviewed = dto.MedicalRecordsReviewed;
        entity.WitnessStatementsReviewed = dto.WitnessStatementsReviewed;
        entity.PoliceReportsReviewed = dto.PoliceReportsReviewed;
        entity.CommanderReportReviewed = dto.CommanderReportReviewed;
        entity.OsiReportsReviewed = dto.OsiReportsReviewed;
        entity.MilitaryPoliceReportsReviewed = dto.MilitaryPoliceReportsReviewed;
        entity.OtherSourcesReviewed = dto.OtherSourcesReviewed;
        entity.OtherSourcesDescription = dto.OtherSourcesDescription;
        entity.MisconductExplanation = dto.MisconductExplanation;
        entity.CommanderToLine = dto.CommanderToLine;
        entity.CommanderFromLine = dto.CommanderFromLine;

        // Commander Review — Duty Status
        entity.WasPresentForDuty = dto.WasPresentForDuty;
        entity.WasOnDuty = dto.WasOnDuty;
        entity.WasOnIDT = dto.WasOnIDT;
        entity.WasAbsentWithLeave = dto.WasAbsentWithLeave;
        entity.WasAbsentWithoutLeave = dto.WasAbsentWithoutLeave;
        entity.AbsentWithoutLeaveDate1 = dto.AbsentWithoutLeaveDate1;
        entity.AbsentWithoutLeaveTime1 = dto.AbsentWithoutLeaveTime1;
        entity.AbsentWithoutLeaveDate2 = dto.AbsentWithoutLeaveDate2;
        entity.AbsentWithoutLeaveTime2 = dto.AbsentWithoutLeaveTime2;

        // Commander Review — Witnesses
        entity.WitnessNameAddress1 = dto.WitnessNameAddress1;
        entity.WitnessNameAddress2 = dto.WitnessNameAddress2;
        entity.WitnessNameAddress3 = dto.WitnessNameAddress3;
        entity.WitnessNameAddress4 = dto.WitnessNameAddress4;
        entity.WitnessNameAddress5 = dto.WitnessNameAddress5;

        // Process Details
        entity.InitiationDate = dto.InitiationDate;
        entity.CompletionDate = dto.CompletionDate;
        entity.TotalTimelineDays = dto.TotalTimelineDays;
        entity.IsInterimLOD = dto.IsInterimLOD;
        entity.InterimLODExpiration = dto.InterimLODExpiration;

        // Findings and Determinations
        entity.FinalFinding = dto.FinalFinding;
        entity.ProximateCause = dto.ProximateCause;
        entity.IsPriorServiceCondition = dto.IsPriorServiceCondition;
        entity.PSCDocumentation = dto.PSCDocumentation;
        entity.EightYearRuleApplies = dto.EightYearRuleApplies;
        entity.YearsOfService = dto.YearsOfService;

        // Signatures — Provider
        entity.ProviderNameRank = dto.ProviderNameRank;
        entity.ProviderDate = dto.ProviderDate;
        entity.ProviderSignature = dto.ProviderSignature;

        // Signatures — Commander
        entity.CommanderNameRank = dto.CommanderNameRank;
        entity.CommanderDate = dto.CommanderDate;
        entity.CommanderSignature = dto.CommanderSignature;

        // SJA/Legal Review
        entity.SjaNameRank = dto.SjaNameRank;
        entity.SjaDate = dto.SjaDate;
        entity.SjaConcurs = dto.SjaConcurs;

        // Wing CC / Appointing Authority
        entity.WingCcSignature = dto.WingCcSignature;
        entity.AppointingAuthorityNameRank = dto.AppointingAuthorityNameRank;
        entity.AppointingAuthorityDate = dto.AppointingAuthorityDate;
        entity.AppointingAuthoritySignature = dto.AppointingAuthoritySignature;

        // Formal Board Review
        entity.MedicalReviewText = dto.MedicalReviewText;
        entity.MedicalReviewerNameRank = dto.MedicalReviewerNameRank;
        entity.MedicalReviewDate = dto.MedicalReviewDate;
        entity.MedicalReviewerSignature = dto.MedicalReviewerSignature;
        entity.LegalReviewText = dto.LegalReviewText;
        entity.LegalReviewerNameRank = dto.LegalReviewerNameRank;
        entity.LegalReviewDate = dto.LegalReviewDate;
        entity.LegalReviewerSignature = dto.LegalReviewerSignature;
        entity.LodBoardChairNameRank = dto.LodBoardChairNameRank;
        entity.LodBoardChairDate = dto.LodBoardChairDate;
        entity.LodBoardChairSignature = dto.LodBoardChairSignature;
        entity.BoardFinding = dto.BoardFinding;
        entity.BoardReferForFormal = dto.BoardReferForFormal;

        // Approving Authority
        entity.ApprovingAuthorityNameRank = dto.ApprovingAuthorityNameRank;
        entity.ApprovingAuthorityDate = dto.ApprovingAuthorityDate;
        entity.ApprovingAuthoritySignature = dto.ApprovingAuthoritySignature;
        entity.ApprovingFinding = dto.ApprovingFinding;
        entity.ApprovingReferForFormal = dto.ApprovingReferForFormal;

        // Notification & Reporting
        entity.NotifiedMedicalUnitTimely = dto.NotifiedMedicalUnitTimely;
        entity.SubmittedMedicalDocumentsTimely = dto.SubmittedMedicalDocumentsTimely;

        // Special Handling
        entity.IsSexualAssaultCase = dto.IsSexualAssaultCase;
        entity.RestrictedReporting = dto.RestrictedReporting;
        entity.SARCCoordination = dto.SARCCoordination;

        entity.ToxicologyReport = dto.ToxicologyReport;

        // Related Benefits
        entity.MEDCONId = dto.MEDCONId;
        entity.INCAPId = dto.INCAPId;
        entity.MemberChoseMEDCON = dto.MemberChoseMEDCON;

        // Member FK
        entity.MemberId = dto.MemberId;

        // Audit flags
        entity.IsAudited = dto.IsAudited;
        entity.PointOfContact = dto.PointOfContact;
    }
}
