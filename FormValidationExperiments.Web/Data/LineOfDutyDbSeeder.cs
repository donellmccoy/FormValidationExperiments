using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Web.Enums;
using FormValidationExperiments.Web.Models;

namespace FormValidationExperiments.Web.Data;

/// <summary>
/// Seeds the in-memory database with realistic sample LOD case data.
/// </summary>
public static class LineOfDutyDbSeeder
{
    public static async Task SeedAsync(IDbContextFactory<LineOfDutyDbContext> contextFactory)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        if (await context.Cases.AnyAsync())
            return;

        var medcon1 = new MEDCONDetails
        {
            IsEligible = true,
            StartDate = new DateTime(2025, 3, 20),
            EndDate = new DateTime(2025, 6, 20),
            ExtensionDays = 0,
            UsesInterimLOD = true,
            TreatmentPlan = "Physical therapy 3x/week for right knee rehabilitation",
            OutOfLocalAreaLeaveApproved = false,
            PhysicianMemo = string.Empty
        };

        var incap1 = new INCAPDetails
        {
            IsEligible = false,
            CivilianIncomeLoss = 0m,
            Documentation = string.Empty
        };

        var medcon2 = new MEDCONDetails
        {
            IsEligible = false,
            ExtensionDays = 0,
            UsesInterimLOD = false,
            TreatmentPlan = string.Empty,
            OutOfLocalAreaLeaveApproved = false,
            PhysicianMemo = string.Empty
        };

        var incap2 = new INCAPDetails
        {
            IsEligible = true,
            CivilianIncomeLoss = 3200.00m,
            StartDate = new DateTime(2025, 5, 15),
            EndDate = new DateTime(2025, 8, 15),
            Documentation = "Employer wage statement and civilian pay stubs provided"
        };

        var commander = new LineOfDutyAuthority
        {
            Role = "Immediate Commander",
            Name = "Col James R. Mitchell",
            Title = "944 FW/CC",
            ActionDate = new DateTime(2025, 4, 5),
            Recommendation = "Line of Duty",
            Comments = new List<string> { "Member was performing official duties at time of injury." }
        };

        var sja = new LineOfDutyAuthority
        {
            Role = "Staff Judge Advocate",
            Name = "Lt Col Sarah Chen",
            Title = "944 FW/JA",
            ActionDate = new DateTime(2025, 4, 10),
            Recommendation = "Legally sufficient",
            Comments = new List<string> { "All documentation reviewed and found legally sufficient." }
        };

        var medicalProvider = new LineOfDutyAuthority
        {
            Role = "Medical Provider",
            Name = "Maj David Park, MD",
            Title = "944 MDG/SGP",
            ActionDate = new DateTime(2025, 3, 18),
            Recommendation = "Treatment required — duty limiting",
            Comments = new List<string> { "Right ACL tear confirmed via MRI.", "Surgical consult recommended." }
        };

        var case1 = new LineOfDutyCase
        {
            CaseId = "LOD-2025-00142",
            ProcessType = LineOfDutyProcessType.Informal,
            Component = ServiceComponent.AirForceReserve,
            MemberName = "TSgt Marcus A. Johnson",
            MemberRank = "E-6",
            ServiceNumber = "123-45-6789",
            Unit = "944th Fighter Wing, Luke AFB, AZ",
            IncidentType = IncidentType.Injury,
            IncidentDate = new DateTime(2025, 3, 15),
            IncidentDescription = "Member sustained right knee injury (ACL tear) during unit physical training session on base.",
            IncidentDutyStatus = DutyStatus.InactiveDutyTraining,
            InitiationDate = new DateTime(2025, 3, 16),
            TotalTimelineDays = 90,
            IsInterimLOD = true,
            InterimLODExpiration = new DateTime(2025, 6, 14),
            FinalFinding = LineOfDutyFinding.InLineOfDuty,
            ProximateCause = string.Empty,
            IsPriorServiceCondition = false,
            PSCDocumentation = string.Empty,
            EightYearRuleApplies = false,
            YearsOfService = 12,
            IsSexualAssaultCase = false,
            RestrictedReporting = false,
            SARCCoordination = string.Empty,
            ToxicologyReport = "Not applicable",
            MemberChoseMEDCON = true,
            IsAudited = false,
            PointOfContact = "944fw.a1@us.af.mil",
            WitnessStatements = new List<string>
            {
                "SSgt Rivera: I witnessed TSgt Johnson fall during the 1.5 mile run.",
                "MSgt Torres: I was the PTL on duty and called for medical assistance immediately."
            },
            AuditComments = new List<string>(),
            MEDCON = medcon1,
            INCAP = incap1,
            Authorities = new List<LineOfDutyAuthority> { commander, sja, medicalProvider },
            Documents = new List<LineOfDutyDocument>
            {
                new LineOfDutyDocument
                {
                    DocumentType = "AF Form 348",
                    FileName = "AF348_Johnson_LOD2025-00142.pdf",
                    UploadDate = new DateTime(2025, 3, 20),
                    Description = "Line of Duty Determination form"
                },
                new LineOfDutyDocument
                {
                    DocumentType = "Medical Records",
                    FileName = "MRI_Report_Johnson_20250318.pdf",
                    UploadDate = new DateTime(2025, 3, 22),
                    Description = "MRI results confirming right ACL tear"
                },
                new LineOfDutyDocument
                {
                    DocumentType = "DD Form 261",
                    FileName = "DD261_Johnson.pdf",
                    UploadDate = new DateTime(2025, 3, 20),
                    Description = "Report of Investigation"
                }
            },
            TimelineSteps = new List<TimelineStep>
            {
                new TimelineStep
                {
                    StepDescription = "Member Reports Injury",
                    TimelineDays = 1,
                    StartDate = new DateTime(2025, 3, 15),
                    CompletionDate = new DateTime(2025, 3, 15),
                    IsOptional = false
                },
                new TimelineStep
                {
                    StepDescription = "Medical Provider Review",
                    TimelineDays = 5,
                    StartDate = new DateTime(2025, 3, 16),
                    CompletionDate = new DateTime(2025, 3, 18),
                    IsOptional = false
                },
                new TimelineStep
                {
                    StepDescription = "Commander Review and Endorsement",
                    TimelineDays = 14,
                    StartDate = new DateTime(2025, 3, 19),
                    CompletionDate = new DateTime(2025, 4, 5),
                    IsOptional = false
                },
                new TimelineStep
                {
                    StepDescription = "Legal/SJA Review",
                    TimelineDays = 10,
                    StartDate = new DateTime(2025, 4, 6),
                    CompletionDate = new DateTime(2025, 4, 10),
                    IsOptional = false
                }
            },
            Appeals = new List<LineOfDutyAppeal>()
        };

        var angCommander = new LineOfDutyAuthority
        {
            Role = "Immediate Commander",
            Name = "Lt Col Andrea Williams",
            Title = "187 AW/CC",
            ActionDate = new DateTime(2025, 5, 20),
            Recommendation = "Not in Line of Duty",
            Comments = new List<string>
            {
                "Member was off-duty and alcohol was a contributing factor.",
                "Toxicology report indicates BAC of 0.12%."
            }
        };

        var case2 = new LineOfDutyCase
        {
            CaseId = "LOD-2025-00287",
            ProcessType = LineOfDutyProcessType.Formal,
            Component = ServiceComponent.AirNationalGuard,
            MemberName = "SrA Kyle T. Brennan",
            MemberRank = "E-4",
            ServiceNumber = "987-65-4321",
            Unit = "187th Attack Wing, Dannelly Field, AL",
            IncidentType = IncidentType.Injury,
            IncidentDate = new DateTime(2025, 5, 10),
            IncidentDescription = "Member involved in single-vehicle motor accident off-base while returning from an off-duty social event. Alcohol was involved.",
            IncidentDutyStatus = DutyStatus.NotInDutyStatus,
            InitiationDate = new DateTime(2025, 5, 12),
            TotalTimelineDays = 160,
            IsInterimLOD = false,
            FinalFinding = LineOfDutyFinding.NotInLineOfDutyDueToMisconduct,
            ProximateCause = "Member's own misconduct — driving under the influence of alcohol",
            IsPriorServiceCondition = false,
            PSCDocumentation = string.Empty,
            EightYearRuleApplies = false,
            YearsOfService = 3,
            IsSexualAssaultCase = false,
            RestrictedReporting = false,
            SARCCoordination = string.Empty,
            ToxicologyReport = "BAC 0.12% at time of accident per civilian law enforcement report",
            MemberChoseMEDCON = false,
            IsAudited = true,
            PointOfContact = "187aw.a1@ang.af.mil",
            WitnessStatements = new List<string>
            {
                "Civilian police report #2025-AL-04821 attached.",
                "Emergency room intake notes reference strong odor of alcohol."
            },
            AuditComments = new List<string>
            {
                "Case reviewed by HQ NGB/A1 — findings upheld."
            },
            MEDCON = medcon2,
            INCAP = incap2,
            Authorities = new List<LineOfDutyAuthority> { angCommander },
            Documents = new List<LineOfDutyDocument>
            {
                new LineOfDutyDocument
                {
                    DocumentType = "AF Form 348",
                    FileName = "AF348_Brennan_LOD2025-00287.pdf",
                    UploadDate = new DateTime(2025, 5, 15),
                    Description = "Line of Duty Determination form — Formal process"
                },
                new LineOfDutyDocument
                {
                    DocumentType = "Police Report",
                    FileName = "CivilianPoliceReport_2025-AL-04821.pdf",
                    UploadDate = new DateTime(2025, 5, 16),
                    Description = "Civilian law enforcement accident report"
                },
                new LineOfDutyDocument
                {
                    DocumentType = "Toxicology Report",
                    FileName = "ToxReport_Brennan_20250510.pdf",
                    UploadDate = new DateTime(2025, 5, 16),
                    Description = "Blood alcohol content results"
                }
            },
            TimelineSteps = new List<TimelineStep>
            {
                new TimelineStep
                {
                    StepDescription = "Member Reports Injury",
                    TimelineDays = 2,
                    StartDate = new DateTime(2025, 5, 10),
                    CompletionDate = new DateTime(2025, 5, 12),
                    IsOptional = false
                },
                new TimelineStep
                {
                    StepDescription = "Commander Review and Endorsement",
                    TimelineDays = 14,
                    StartDate = new DateTime(2025, 5, 13),
                    CompletionDate = new DateTime(2025, 5, 20),
                    IsOptional = false
                },
                new TimelineStep
                {
                    StepDescription = "Formal Board Review",
                    TimelineDays = 30,
                    StartDate = new DateTime(2025, 5, 21),
                    IsOptional = false
                }
            },
            Appeals = new List<LineOfDutyAppeal>
            {
                new LineOfDutyAppeal
                {
                    AppealDate = new DateTime(2025, 6, 25),
                    Appellant = "SrA Kyle T. Brennan",
                    NewEvidence = new List<string>
                    {
                        "Member statement contesting BAC level accuracy",
                        "Independent toxicology lab retest results"
                    },
                    OriginalFinding = LineOfDutyFinding.NotInLineOfDutyDueToMisconduct,
                    AppealOutcome = LineOfDutyFinding.NotInLineOfDutyDueToMisconduct,
                    ResolutionDate = new DateTime(2025, 8, 10)
                }
            }
        };

        context.Cases.AddRange(case1, case2);
        await context.SaveChangesAsync();
    }
}
