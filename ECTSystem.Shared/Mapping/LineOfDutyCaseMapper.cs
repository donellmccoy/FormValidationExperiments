using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Static mapper for converting between <see cref="LineOfDutyCase"/> domain model
/// and the consolidated <see cref="LineOfDutyViewModel"/> used by the LOD workflow forms.
/// </summary>
public static partial class LineOfDutyCaseMapper
{
    [GeneratedRegex(@"^(AB|Amn|A1C|SrA|SSgt|TSgt|MSgt|SMSgt|CMSgt|CMSAF|2d Lt|1st Lt|Capt|Maj|Lt Col|Col|Brig Gen|Maj Gen|Lt Gen|Gen)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex RankPrefixPattern();

    // ──────────────────────────── Domain → View Model ────────────────────────────

    public static LineOfDutyViewModel ToLineOfDutyViewModel(LineOfDutyCase source)
    {
        var commander = FindAuthority(source, "Immediate Commander");
        var medProvider = FindAuthority(source, "Medical Provider");
        var sja = FindAuthority(source, "Staff Judge Advocate");

        ParseMemberName(source.MemberName, out var lastName, out var firstName, out var middleInitial);

        var parsedRank = ParseMilitaryRank(source.MemberRank);
        var hasToxReport = !string.IsNullOrWhiteSpace(source.ToxicologyReport)
                           && !source.ToxicologyReport.Equals("Not applicable", StringComparison.OrdinalIgnoreCase);
        var isLegallySufficient = sja?.Recommendation?.Contains("sufficient", StringComparison.OrdinalIgnoreCase) == true;

        return new LineOfDutyViewModel
        {
            CaseNumber = source.CaseId ?? string.Empty,
            MemberName = source.MemberName ?? string.Empty,
            Component = source.Component.ToDisplayString(),
            Rank = parsedRank is { } rankForDisplay
                ? FormatRankToFullName(rankForDisplay)
                : source.MemberRank ?? string.Empty,
            Grade = parsedRank is { } rankForGrade
                ? FormatRankToPayGrade(rankForGrade)
                : string.Empty,
            Unit = source.Unit ?? string.Empty,
            DateOfInjury = source.IncidentDate.ToString("yyyy-MM-dd"),
            SSN = source.ServiceNumber ?? string.Empty,
            DutyStatus = source.IncidentDutyStatus.ToDisplayString(),
            Status = DeriveStatus(source),
            IncidentCircumstances = source.IncidentDescription ?? string.Empty,
            ReportedInjury = source.IncidentDescription ?? string.Empty,

            RequestingCommander = commander?.Name ?? string.Empty,
            MedicalProvider = medProvider?.Name ?? string.Empty,
            ReportDate = source.InitiationDate,
            LastName = lastName,
            FirstName = firstName,
            MiddleInitial = middleInitial,
            DateOfBirth = source.MemberDateOfBirth ?? source.Member?.DateOfBirth,
            OrganizationUnit = source.Unit ?? string.Empty,
            MemberStatus = MapComponentToMemberStatus(source.Component) is { } ms ? ms.ToString() : string.Empty,
            NotifiedMedicalUnitTimely = source.NotifiedMedicalUnitTimely,
            SubmittedMedicalDocumentsTimely = source.SubmittedMedicalDocumentsTimely,
            InvolvesSexualAssault = source.IsSexualAssaultCase,
            IsRestrictedReport = source.RestrictedReporting,

            InvestigationType = source.IncidentType,
            IsMilitaryFacility = source.IsMilitaryFacility,
            TreatmentFacilityName = source.TreatmentFacilityName ?? string.Empty,
            TreatmentDateTime = source.TreatmentDateTime,
            ClinicalDiagnosis = source.ClinicalDiagnosis ?? string.Empty,
            MedicalFindings = source.MedicalFindings ?? string.Empty,
            WasUnderInfluence = source.WasUnderInfluence,
            SubstanceType = source.SubstanceType,
            ToxicologyTestDone = hasToxReport ? true : null,
            ToxicologyTestResults = hasToxReport ? source.ToxicologyReport ?? string.Empty : string.Empty,
            WasMentallyResponsible = source.WasMentallyResponsible,
            PsychiatricEvalCompleted = source.PsychiatricEvalCompleted,
            PsychiatricEvalDate = source.PsychiatricEvalDate,
            PsychiatricEvalResults = source.PsychiatricEvalResults ?? string.Empty,
            OtherRelevantConditions = source.OtherRelevantConditions ?? string.Empty,
            OtherTestsDone = source.OtherTestsDone,
            OtherTestDate = source.OtherTestDate,
            OtherTestResults = source.OtherTestResults ?? string.Empty,
            IsEptsNsa = source.IsPriorServiceCondition ? true : false,
            IsServiceAggravated = source.IsServiceAggravated,
            IsPotentiallyUnfitting = source.IsPotentiallyUnfitting,
            IsAtDeployedLocation = source.IsAtDeployedLocation,
            RequiresArcBoard = source.RequiresArcBoard,
            MedicalRecommendation = source.MedicalRecommendation ?? string.Empty,

            MemberStatementReviewed = source.MemberStatementReviewed,
            MedicalRecordsReviewed = source.MedicalRecordsReviewed,
            WitnessStatementsReviewed = source.WitnessStatementsReviewed,
            PoliceReportsReviewed = source.PoliceReportsReviewed,
            CommanderReportReviewed = source.CommanderReportReviewed,
            OtherSourcesReviewed = source.OtherSourcesReviewed,
            OtherSourcesDescription = source.OtherSourcesDescription ?? string.Empty,
            DutyStatusAtTime = source.IncidentDutyStatus,
            NarrativeOfCircumstances = source.IncidentDescription ?? string.Empty,
            ResultOfMisconduct = source.FinalFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ? true
                               : source.FinalFinding == LineOfDutyFinding.InLineOfDuty ? false
                               : null,
            MisconductExplanation = source.MisconductExplanation ?? string.Empty,
            ProximateCause = source.ProximateCause ?? string.Empty,
            Recommendation = MapFindingToRecommendation(source.FinalFinding),
            RecommendationRemarks = commander?.Comments != null ? string.Join(" ", commander.Comments) : string.Empty,
            CommanderName = commander?.Name ?? string.Empty,
            CommanderRank = ParseMilitaryRank(commander?.Rank),
            CommanderOrganization = commander?.Title ?? string.Empty,
            CommanderSignatureDate = commander?.ActionDate,

            IsLegallySufficient = sja != null ? isLegallySufficient : null,
            ConcurWithRecommendation = sja != null ? true : null,
            LegalRemarks = sja?.Comments != null ? string.Join(" ", sja.Comments) : string.Empty,
            SJAName = sja?.Name ?? string.Empty,
            SJARank = ParseMilitaryRank(sja?.Rank),
            SJAOrganization = sja?.Title ?? string.Empty,
            SJASignatureDate = sja?.ActionDate,
        };
    }

    // ──────────────────────────── View Model → Domain ────────────────────────────

    public static void ApplyToCase(LineOfDutyViewModel model, LineOfDutyCase target)
    {
        var nameParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(model.FirstName))
        {
            nameParts.Add(model.FirstName);
        }

        if (!string.IsNullOrWhiteSpace(model.MiddleInitial))
        {
            nameParts.Add(model.MiddleInitial + ".");
        }

        if (!string.IsNullOrWhiteSpace(model.LastName))
        {
            nameParts.Add(model.LastName);
        }

        target.MemberName = string.Join(" ", nameParts);
        target.ServiceNumber = model.SSN ?? string.Empty;
        target.MemberDateOfBirth = model.DateOfBirth;
        target.MemberRank = model.Rank ?? string.Empty;
        target.Unit = model.OrganizationUnit;
        target.InitiationDate = model.ReportDate ?? target.InitiationDate;

        if (!string.IsNullOrWhiteSpace(model.MemberStatus)
            && Enum.TryParse<MemberStatus>(model.MemberStatus, ignoreCase: true, out var memberStatus))
        {
            target.Component = MapMemberStatusToComponent(memberStatus);
        }

        target.NotifiedMedicalUnitTimely = model.NotifiedMedicalUnitTimely;
        target.SubmittedMedicalDocumentsTimely = model.SubmittedMedicalDocumentsTimely;
        target.IsSexualAssaultCase = model.InvolvesSexualAssault;
        target.RestrictedReporting = model.IsRestrictedReport;

        target.IncidentType = model.InvestigationType ?? target.IncidentType;
        target.IsMilitaryFacility = model.IsMilitaryFacility;
        target.TreatmentFacilityName = model.TreatmentFacilityName;
        target.TreatmentDateTime = model.TreatmentDateTime;
        target.ClinicalDiagnosis = model.ClinicalDiagnosis;
        target.MedicalFindings = model.MedicalFindings;
        target.WasUnderInfluence = model.WasUnderInfluence;
        target.SubstanceType = model.SubstanceType;
        target.WasMentallyResponsible = model.WasMentallyResponsible;
        target.PsychiatricEvalCompleted = model.PsychiatricEvalCompleted;
        target.PsychiatricEvalDate = model.PsychiatricEvalDate;
        target.PsychiatricEvalResults = model.PsychiatricEvalResults;
        target.OtherRelevantConditions = model.OtherRelevantConditions;
        target.OtherTestsDone = model.OtherTestsDone;
        target.OtherTestDate = model.OtherTestDate;
        target.OtherTestResults = model.OtherTestResults;
        target.IsPriorServiceCondition = model.IsEptsNsa == true;
        target.IsServiceAggravated = model.IsServiceAggravated;
        target.IsPotentiallyUnfitting = model.IsPotentiallyUnfitting;
        target.IsAtDeployedLocation = model.IsAtDeployedLocation;
        target.RequiresArcBoard = model.RequiresArcBoard;
        target.MedicalRecommendation = model.MedicalRecommendation;
        target.ToxicologyReport = model.ToxicologyTestDone == true ? model.ToxicologyTestResults : "Not applicable";

        target.MemberStatementReviewed = model.MemberStatementReviewed;
        target.MedicalRecordsReviewed = model.MedicalRecordsReviewed;
        target.WitnessStatementsReviewed = model.WitnessStatementsReviewed;
        target.PoliceReportsReviewed = model.PoliceReportsReviewed;
        target.CommanderReportReviewed = model.CommanderReportReviewed;
        target.OtherSourcesReviewed = model.OtherSourcesReviewed;
        target.OtherSourcesDescription = model.OtherSourcesDescription;
        target.IncidentDutyStatus = model.DutyStatusAtTime ?? target.IncidentDutyStatus;
        target.IncidentDescription = model.NarrativeOfCircumstances;
        target.FinalFinding = model.Recommendation.HasValue ? MapRecommendationToFinding(model.Recommendation.Value) : target.FinalFinding;
        target.MisconductExplanation = model.MisconductExplanation;
        target.ProximateCause = model.ProximateCause;

        var commander = FindOrCreateAuthority(target, "Immediate Commander");
        commander.Name = model.CommanderName;
        commander.Rank = model.CommanderRank.HasValue ? model.CommanderRank.Value.ToString() : string.Empty;
        commander.ActionDate = model.CommanderSignatureDate;
        commander.Title = model.CommanderOrganization;
        commander.Comments = string.IsNullOrWhiteSpace(model.RecommendationRemarks)
            ? []
            : [model.RecommendationRemarks];

        var medProvider = FindOrCreateAuthority(target, "Medical Provider");
        medProvider.Name = model.MedicalProvider;

        var sja = FindOrCreateAuthority(target, "Staff Judge Advocate");
        sja.Name = model.SJAName;
        sja.Rank = model.SJARank.HasValue ? model.SJARank.Value.ToString() : string.Empty;
        sja.ActionDate = model.SJASignatureDate;
        sja.Title = model.SJAOrganization;
        sja.Recommendation = model.IsLegallySufficient == true ? "Legally sufficient" : "Legally insufficient";

        var sjaRemarks = new List<string>();
        if (!string.IsNullOrWhiteSpace(model.LegalRemarks))
        {
            sjaRemarks.Add(model.LegalRemarks);
        }

        if (model.ConcurWithRecommendation == false && !string.IsNullOrWhiteSpace(model.NonConcurrenceReason))
        {
            sjaRemarks.Add($"Non-concurrence: {model.NonConcurrenceReason}");
        }

        sja.Comments = sjaRemarks;
    }

    // ──────────────────────────── Helper Methods ────────────────────────────

    private static LineOfDutyAuthority FindOrCreateAuthority(LineOfDutyCase source, string role)
    {
        var authority = FindAuthority(source, role);
        if (authority is null)
        {
            authority = new LineOfDutyAuthority { Role = role };
            source.Authorities ??= [];
            source.Authorities.Add(authority);
        }
        return authority;
    }

    public static string FormatRankToPayGrade(MilitaryRank rank)
    {
        return rank switch
        {
            MilitaryRank.AB => "E-1",
            MilitaryRank.Amn => "E-2",
            MilitaryRank.A1C => "E-3",
            MilitaryRank.SrA => "E-4",
            MilitaryRank.SSgt => "E-5",
            MilitaryRank.TSgt => "E-6",
            MilitaryRank.MSgt => "E-7",
            MilitaryRank.SMSgt => "E-8",
            MilitaryRank.CMSgt => "E-9",
            MilitaryRank.SecondLt => "O-1",
            MilitaryRank.FirstLt => "O-2",
            MilitaryRank.Capt => "O-3",
            MilitaryRank.Maj => "O-4",
            MilitaryRank.LtCol => "O-5",
            MilitaryRank.Col => "O-6",
            MilitaryRank.BrigGen => "O-7",
            MilitaryRank.MajGen => "O-8",
            MilitaryRank.LtGen => "O-9",
            MilitaryRank.Gen => "O-10",
            _ => rank.ToString()
        };
    }

    public static string FormatRankToFullName(MilitaryRank rank)
    {
        return rank switch
        {
            MilitaryRank.AB => "Airman Basic",
            MilitaryRank.Amn => "Airman",
            MilitaryRank.A1C => "Airman First Class",
            MilitaryRank.SrA => "Senior Airman",
            MilitaryRank.SSgt => "Staff Sergeant",
            MilitaryRank.TSgt => "Technical Sergeant",
            MilitaryRank.MSgt => "Master Sergeant",
            MilitaryRank.SMSgt => "Senior Master Sergeant",
            MilitaryRank.CMSgt => "Chief Master Sergeant",
            MilitaryRank.SecondLt => "Second Lieutenant",
            MilitaryRank.FirstLt => "First Lieutenant",
            MilitaryRank.Capt => "Captain",
            MilitaryRank.Maj => "Major",
            MilitaryRank.LtCol => "Lieutenant Colonel",
            MilitaryRank.Col => "Colonel",
            MilitaryRank.BrigGen => "Brigadier General",
            MilitaryRank.MajGen => "Major General",
            MilitaryRank.LtGen => "Lieutenant General",
            MilitaryRank.Gen => "General",
            MilitaryRank.Cadet => "Cadet",
            _ => rank.ToString()
        };
    }

    private static ServiceComponent MapMemberStatusToComponent(MemberStatus status)
    {
        return status switch
        {
            MemberStatus.AFR => ServiceComponent.AirForceReserve,
            MemberStatus.ANG => ServiceComponent.AirNationalGuard,
            _ => ServiceComponent.RegularAirForce
        };
    }

    private static LineOfDutyFinding MapRecommendationToFinding(CommanderRecommendation recommendation)
    {
        return recommendation switch
        {
            CommanderRecommendation.InLineOfDuty => LineOfDutyFinding.InLineOfDuty,
            CommanderRecommendation.NotInLineOfDutyDueToMisconduct => LineOfDutyFinding.NotInLineOfDutyDueToMisconduct,
            CommanderRecommendation.NotInLineOfDutyNotDueToMisconduct => LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct,
            _ => LineOfDutyFinding.InLineOfDuty
        };
    }

    private static LineOfDutyAuthority FindAuthority(LineOfDutyCase source, string role)
    {
        return source.Authorities?.FirstOrDefault(a =>
            a.Role != null && a.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    private static string MaskSsn(string serviceNumber)
    {
        if (string.IsNullOrWhiteSpace(serviceNumber))
        {
            return string.Empty;
        }

        // If already in XXX-XX-XXXX format, mask the first 5 digits
        if (serviceNumber.Length >= 9)
        {
            var digits = serviceNumber.Replace("-", "");
            if (digits.Length >= 9)
            {
                return $"***-**-{digits[^4..]}";
            }
        }

        return serviceNumber;
    }

    private static void ParseMemberName(string fullName, out string lastName, out string firstName, out string middleInitial)
    {
        lastName = string.Empty;
        firstName = string.Empty;
        middleInitial = string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return;
        }

        // Expected formats:
        //   "TSgt Marcus A. Johnson"  → rank prefix + first middle last
        //   "John Doe"               → first last
        //   "Doe, John A."           → last, first middle

        // Strip common rank prefixes
        var name = RankPrefixPattern().Replace(fullName, "").Trim();

        if (name.Contains(','))
        {
            // "Last, First M."
            var parts = name.Split(',', 2);
            lastName = parts[0].Trim();
            var rest = parts.Length > 1 ? parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
            if (rest.Length >= 1)
            {
                firstName = rest[0];
            }

            if (rest.Length >= 2)
            {
                middleInitial = rest[1].TrimEnd('.');
            }
        }
        else
        {
            // "First M. Last" or "First Last"
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                lastName = parts[0];
            }
            else if (parts.Length == 2)
            {
                firstName = parts[0];
                lastName = parts[1];
            }
            else
            {
                firstName = parts[0];
                middleInitial = parts[1].TrimEnd('.');
                lastName = string.Join(" ", parts.Skip(2));
            }
        }
    }

    public static MilitaryRank? ParseMilitaryRank(string rankString)
    {
        if (string.IsNullOrWhiteSpace(rankString))
        {
            return null;
        }

        // Try exact match against enum names
        if (Enum.TryParse<MilitaryRank>(rankString, ignoreCase: true, out var rank))
        {
            return rank;
        }

        // Map common abbreviations / pay grades to enum values
        return rankString.Trim().ToUpperInvariant() switch
        {
            "AB" or "E-1" => MilitaryRank.AB,
            "AMN" or "E-2" => MilitaryRank.Amn,
            "A1C" or "E-3" => MilitaryRank.A1C,
            "SRA" or "E-4" => MilitaryRank.SrA,
            "SSGT" or "E-5" => MilitaryRank.SSgt,
            "TSGT" or "E-6" => MilitaryRank.TSgt,
            "MSGT" or "E-7" => MilitaryRank.MSgt,
            "SMSGT" or "E-8" => MilitaryRank.SMSgt,
            "CMSGT" or "E-9" => MilitaryRank.CMSgt,
            "2D LT" or "2LT" or "O-1" => MilitaryRank.SecondLt,
            "1ST LT" or "1LT" or "O-2" => MilitaryRank.FirstLt,
            "CAPT" or "O-3" => MilitaryRank.Capt,
            "MAJ" or "O-4" => MilitaryRank.Maj,
            "LT COL" or "O-5" => MilitaryRank.LtCol,
            "COL" or "O-6" => MilitaryRank.Col,
            "BRIG GEN" or "O-7" => MilitaryRank.BrigGen,
            "MAJ GEN" or "O-8" => MilitaryRank.MajGen,
            "LT GEN" or "O-9" => MilitaryRank.LtGen,
            "GEN" or "O-10" => MilitaryRank.Gen,
            _ => null
        };
    }

    private static MemberStatus? MapComponentToMemberStatus(ServiceComponent component)
    {
        return component switch
        {
            ServiceComponent.AirForceReserve => MemberStatus.AFR,
            ServiceComponent.AirNationalGuard => MemberStatus.ANG,
            _ => null
        };
    }

    private static CommanderRecommendation? MapFindingToRecommendation(LineOfDutyFinding finding)
    {
        return finding switch
        {
            LineOfDutyFinding.InLineOfDuty => CommanderRecommendation.InLineOfDuty,
            LineOfDutyFinding.NotInLineOfDutyDueToMisconduct => CommanderRecommendation.NotInLineOfDutyDueToMisconduct,
            LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct => CommanderRecommendation.NotInLineOfDutyNotDueToMisconduct,
            _ => null
        };
    }

    private static string DeriveStatus(LineOfDutyCase source)
    {
        if (source.CompletionDate.HasValue)
        {
            return "Completed";
        }

        if (source.IsInterimLOD)
        {
            return "Interim LOD";
        }

        return "In Progress";
    }

}
