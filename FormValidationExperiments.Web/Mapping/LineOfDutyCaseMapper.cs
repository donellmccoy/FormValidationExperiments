using System.Text.RegularExpressions;
using FormValidationExperiments.Web.Enums;
using FormValidationExperiments.Web.Models;
using FormValidationExperiments.Web.ViewModels;

namespace FormValidationExperiments.Web.Mapping;

/// <summary>
/// Static mapper for converting between <see cref="LineOfDutyCase"/> domain model
/// and the per-step view models used by the LOD workflow forms.
/// </summary>
public static class LineOfDutyCaseMapper
{
    // ──────────────────────────── Domain → View Models ────────────────────────────

    /// <summary>
    /// Maps a <see cref="LineOfDutyCase"/> to the read-only <see cref="CaseInfoModel"/> header card.
    /// </summary>
    public static CaseInfoModel ToCaseInfoModel(LineOfDutyCase source)
    {
        return new CaseInfoModel
        {
            CaseNumber = source.CaseId ?? string.Empty,
            MemberName = source.MemberName ?? string.Empty,
            Rank = source.MemberRank ?? string.Empty,
            Unit = source.Unit ?? string.Empty,
            DateOfInjury = source.IncidentDate.ToString("yyyy-MM-dd"),
            SSN = MaskSsn(source.ServiceNumber),
            DutyStatus = FormatEnum(source.IncidentDutyStatus),
            Status = DeriveStatus(source),
            IncidentCircumstances = source.IncidentDescription ?? string.Empty,
            ReportedInjury = source.IncidentDescription ?? string.Empty
        };
    }

    /// <summary>
    /// Maps a <see cref="LineOfDutyCase"/> to the <see cref="MemberInfoFormModel"/> (AF Form 348, Items 1–8).
    /// </summary>
    public static MemberInfoFormModel ToMemberInfoFormModel(LineOfDutyCase source)
    {
        var commander = FindAuthority(source, "Immediate Commander");
        var medProvider = FindAuthority(source, "Medical Provider");

        ParseMemberName(source.MemberName, out var lastName, out var firstName, out var middleInitial);

        return new MemberInfoFormModel
        {
            RequestingCommander = commander?.Name ?? string.Empty,
            MedicalProvider = medProvider?.Name ?? string.Empty,
            ReportDate = source.InitiationDate,
            LastName = lastName,
            FirstName = firstName,
            MiddleInitial = middleInitial,
            SSN = ExtractLastFourSsn(source.ServiceNumber),
            Rank = ParseMilitaryRank(source.MemberRank),
            OrganizationUnit = source.Unit ?? string.Empty,
            MemberStatus = MapComponentToMemberStatus(source.Component)
        };
    }

    /// <summary>
    /// Maps a <see cref="LineOfDutyCase"/> to the <see cref="MedicalAssessmentFormModel"/> (AF Form 348, Items 9–15).
    /// </summary>
    public static MedicalAssessmentFormModel ToMedicalAssessmentFormModel(LineOfDutyCase source)
    {
        var medProvider = FindAuthority(source, "Medical Provider");
        var hasToxReport = !string.IsNullOrWhiteSpace(source.ToxicologyReport)
                           && !source.ToxicologyReport.Equals("Not applicable", StringComparison.OrdinalIgnoreCase);

        return new MedicalAssessmentFormModel
        {
            InvestigationType = source.IncidentType,
            ToxicologyTestDone = hasToxReport ? true : null,
            ToxicologyTestResults = hasToxReport ? source.ToxicologyReport ?? string.Empty : string.Empty,
            IsEptsNsa = source.IsPriorServiceCondition ? true : null,
            ProviderName = medProvider?.Name ?? string.Empty,
            ProviderSignatureDate = medProvider?.ActionDate,
            ProviderOrganization = medProvider?.Title ?? string.Empty
        };
    }

    /// <summary>
    /// Maps a <see cref="LineOfDutyCase"/> to the <see cref="CommanderReviewFormModel"/> (AF Form 348, Items 16–23).
    /// </summary>
    public static CommanderReviewFormModel ToCommanderReviewFormModel(LineOfDutyCase source)
    {
        var commander = FindAuthority(source, "Immediate Commander");

        return new CommanderReviewFormModel
        {
            DutyStatusAtTime = source.IncidentDutyStatus,
            NarrativeOfCircumstances = source.IncidentDescription ?? string.Empty,
            ProximateCause = source.ProximateCause ?? string.Empty,
            ResultOfMisconduct = source.FinalFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ? true
                               : source.FinalFinding == LineOfDutyFinding.InLineOfDuty ? false
                               : null,
            Recommendation = MapFindingToRecommendation(source.FinalFinding),
            RecommendationRemarks = commander?.Comments != null ? string.Join(" ", commander.Comments) : string.Empty,
            CommanderName = commander?.Name ?? string.Empty,
            CommanderRank = ParseMilitaryRank(commander?.Title),
            CommanderOrganization = commander?.Title ?? string.Empty,
            CommanderSignatureDate = commander?.ActionDate
        };
    }

    /// <summary>
    /// Maps a <see cref="LineOfDutyCase"/> to the <see cref="LegalSJAReviewFormModel"/> (AF Form 348, Items 24–25).
    /// </summary>
    public static LegalSJAReviewFormModel ToLegalSJAReviewFormModel(LineOfDutyCase source)
    {
        var sja = FindAuthority(source, "Staff Judge Advocate");
        var isLegallySufficient = sja?.Recommendation?.Contains("sufficient", StringComparison.OrdinalIgnoreCase) == true;

        return new LegalSJAReviewFormModel
        {
            IsLegallySufficient = sja != null ? isLegallySufficient : null,
            ConcurWithRecommendation = sja != null ? true : null,
            LegalRemarks = sja?.Comments != null ? string.Join(" ", sja.Comments) : string.Empty,
            SJAName = sja?.Name ?? string.Empty,
            SJAOrganization = sja?.Title ?? string.Empty,
            SJASignatureDate = sja?.ActionDate
        };
    }

    // ──────────────────────────── Helper Methods ────────────────────────────

    private static LineOfDutyAuthority FindAuthority(LineOfDutyCase source, string role)
    {
        return source.Authorities?.FirstOrDefault(a =>
            a.Role != null && a.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    private static string MaskSsn(string serviceNumber)
    {
        if (string.IsNullOrWhiteSpace(serviceNumber))
            return string.Empty;

        // If already in XXX-XX-XXXX format, mask the first 5 digits
        if (serviceNumber.Length >= 9)
        {
            var digits = serviceNumber.Replace("-", "");
            if (digits.Length >= 9)
                return $"***-**-{digits[^4..]}";
        }

        return serviceNumber;
    }

    private static string ExtractLastFourSsn(string serviceNumber)
    {
        if (string.IsNullOrWhiteSpace(serviceNumber))
            return string.Empty;

        var digits = serviceNumber.Replace("-", "");
        return digits.Length >= 4 ? digits[^4..] : digits;
    }

    private static void ParseMemberName(string fullName, out string lastName, out string firstName, out string middleInitial)
    {
        lastName = string.Empty;
        firstName = string.Empty;
        middleInitial = string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
            return;

        // Expected formats:
        //   "TSgt Marcus A. Johnson"  → rank prefix + first middle last
        //   "John Doe"               → first last
        //   "Doe, John A."           → last, first middle

        // Strip common rank prefixes
        var name = Regex.Replace(fullName, @"^(AB|Amn|A1C|SrA|SSgt|TSgt|MSgt|SMSgt|CMSgt|CMSAF|2d Lt|1st Lt|Capt|Maj|Lt Col|Col|Brig Gen|Maj Gen|Lt Gen|Gen)\s+", "", RegexOptions.IgnoreCase).Trim();

        if (name.Contains(','))
        {
            // "Last, First M."
            var parts = name.Split(',', 2);
            lastName = parts[0].Trim();
            var rest = parts.Length > 1 ? parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
            if (rest.Length >= 1) firstName = rest[0];
            if (rest.Length >= 2) middleInitial = rest[1].TrimEnd('.');
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

    private static MilitaryRank? ParseMilitaryRank(string rankString)
    {
        if (string.IsNullOrWhiteSpace(rankString))
            return null;

        // Try exact match against enum names
        if (Enum.TryParse<MilitaryRank>(rankString, ignoreCase: true, out var rank))
            return rank;

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
            return "Completed";

        if (source.IsInterimLOD)
            return "Interim LOD";

        return "In Progress";
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
