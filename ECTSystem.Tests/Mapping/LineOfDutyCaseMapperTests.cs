using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Mapping;

/// <summary>
/// Unit tests for <see cref="LineOfDutyCaseMapper"/>, the static mapper responsible for
/// converting between <see cref="LineOfDutyCase"/> domain entities and the consolidated
/// <see cref="LineOfDutyViewModel"/> used by the LOD workflow forms.
/// </summary>
/// <remarks>
/// <para>
/// The mapper contains both public methods (<see cref="LineOfDutyCaseMapper.FormatRankToPayGrade"/>,
/// <see cref="LineOfDutyCaseMapper.FormatRankToFullName"/>, <see cref="LineOfDutyCaseMapper.ParseMilitaryRank"/>)
/// and private helpers (ParseMemberName, MaskSsn, DeriveStatus, etc.) that are tested indirectly
/// through the public <see cref="LineOfDutyCaseMapper.ToLineOfDutyViewModel"/> and
/// <see cref="LineOfDutyCaseMapper.ApplyToCase"/> round-trip methods.
/// </para>
/// </remarks>
public class LineOfDutyCaseMapperTests
{
    /// <summary>Creates a minimal <see cref="LineOfDutyCase"/> with required defaults set.</summary>
    private static LineOfDutyCase CreateMinimalCase()
    {
        return new LineOfDutyCase
        {
            Id = 1,
            CaseId = "LOD-2025-001",
            MemberName = "Doe, John A.",
            MemberRank = "TSgt",
            ServiceNumber = "123456789",
            Unit = "99 SFS/S3",
            IncidentDate = new DateTime(2025, 3, 15),
            IncidentDutyStatus = DutyStatus.Title10ActiveDuty,
            Component = ServiceComponent.RegularAirForce,
            Authorities = new List<LineOfDutyAuthority>()
        };
    }

    // FormatRankToPayGrade - Enlisted

    /// <summary>
    /// Verifies that every enlisted rank maps to the correct pay grade string (E-1 through E-9).
    /// </summary>
    [Theory]
    [InlineData(MilitaryRank.AB, "E-1")]
    [InlineData(MilitaryRank.Amn, "E-2")]
    [InlineData(MilitaryRank.A1C, "E-3")]
    [InlineData(MilitaryRank.SrA, "E-4")]
    [InlineData(MilitaryRank.SSgt, "E-5")]
    [InlineData(MilitaryRank.TSgt, "E-6")]
    [InlineData(MilitaryRank.MSgt, "E-7")]
    [InlineData(MilitaryRank.SMSgt, "E-8")]
    [InlineData(MilitaryRank.CMSgt, "E-9")]
    public void FormatRankToPayGrade_EnlistedRanks_ReturnsCorrectGrade(MilitaryRank rank, string expected)
    {
        var result = LineOfDutyCaseMapper.FormatRankToPayGrade(rank);
        Assert.Equal(expected, result);
    }

    // FormatRankToPayGrade - Officer

    /// <summary>
    /// Verifies that every officer rank maps to the correct pay grade string (O-1 through O-10).
    /// </summary>
    [Theory]
    [InlineData(MilitaryRank.SecondLt, "O-1")]
    [InlineData(MilitaryRank.FirstLt, "O-2")]
    [InlineData(MilitaryRank.Capt, "O-3")]
    [InlineData(MilitaryRank.Maj, "O-4")]
    [InlineData(MilitaryRank.LtCol, "O-5")]
    [InlineData(MilitaryRank.Col, "O-6")]
    [InlineData(MilitaryRank.BrigGen, "O-7")]
    [InlineData(MilitaryRank.MajGen, "O-8")]
    [InlineData(MilitaryRank.LtGen, "O-9")]
    [InlineData(MilitaryRank.Gen, "O-10")]
    public void FormatRankToPayGrade_OfficerRanks_ReturnsCorrectGrade(MilitaryRank rank, string expected)
    {
        var result = LineOfDutyCaseMapper.FormatRankToPayGrade(rank);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that the Cadet rank (which has no pay grade mapping) falls through to the
    /// default ToString() representation.
    /// </summary>
    [Fact]
    public void FormatRankToPayGrade_Cadet_ReturnsCadetString()
    {
        var result = LineOfDutyCaseMapper.FormatRankToPayGrade(MilitaryRank.Cadet);
        Assert.Equal("Cadet", result);
    }

    // FormatRankToFullName - Enlisted

    /// <summary>
    /// Verifies that every enlisted rank maps to its full display name.
    /// </summary>
    [Theory]
    [InlineData(MilitaryRank.AB, "Airman Basic")]
    [InlineData(MilitaryRank.Amn, "Airman")]
    [InlineData(MilitaryRank.A1C, "Airman First Class")]
    [InlineData(MilitaryRank.SrA, "Senior Airman")]
    [InlineData(MilitaryRank.SSgt, "Staff Sergeant")]
    [InlineData(MilitaryRank.TSgt, "Technical Sergeant")]
    [InlineData(MilitaryRank.MSgt, "Master Sergeant")]
    [InlineData(MilitaryRank.SMSgt, "Senior Master Sergeant")]
    [InlineData(MilitaryRank.CMSgt, "Chief Master Sergeant")]
    public void FormatRankToFullName_EnlistedRanks_ReturnsCorrectFullName(MilitaryRank rank, string expected)
    {
        var result = LineOfDutyCaseMapper.FormatRankToFullName(rank);
        Assert.Equal(expected, result);
    }

    // FormatRankToFullName - Officer

    /// <summary>
    /// Verifies that every officer rank maps to its full display name.
    /// </summary>
    [Theory]
    [InlineData(MilitaryRank.SecondLt, "Second Lieutenant")]
    [InlineData(MilitaryRank.FirstLt, "First Lieutenant")]
    [InlineData(MilitaryRank.Capt, "Captain")]
    [InlineData(MilitaryRank.Maj, "Major")]
    [InlineData(MilitaryRank.LtCol, "Lieutenant Colonel")]
    [InlineData(MilitaryRank.Col, "Colonel")]
    [InlineData(MilitaryRank.BrigGen, "Brigadier General")]
    [InlineData(MilitaryRank.MajGen, "Major General")]
    [InlineData(MilitaryRank.LtGen, "Lieutenant General")]
    [InlineData(MilitaryRank.Gen, "General")]
    public void FormatRankToFullName_OfficerRanks_ReturnsCorrectFullName(MilitaryRank rank, string expected)
    {
        var result = LineOfDutyCaseMapper.FormatRankToFullName(rank);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that the Cadet rank maps to the "Cadet" full name string.
    /// </summary>
    [Fact]
    public void FormatRankToFullName_Cadet_ReturnsCadet()
    {
        var result = LineOfDutyCaseMapper.FormatRankToFullName(MilitaryRank.Cadet);
        Assert.Equal("Cadet", result);
    }

    // ParseMilitaryRank - Exact Enum Names

    /// <summary>
    /// Verifies that exact enum name strings are parsed correctly.
    /// </summary>
    [Theory]
    [InlineData("TSgt", MilitaryRank.TSgt)]
    [InlineData("Col", MilitaryRank.Col)]
    [InlineData("Capt", MilitaryRank.Capt)]
    [InlineData("Cadet", MilitaryRank.Cadet)]
    public void ParseMilitaryRank_ExactEnumName_ReturnsRank(string input, MilitaryRank expected)
    {
        var result = LineOfDutyCaseMapper.ParseMilitaryRank(input);
        Assert.Equal(expected, result);
    }

    // ParseMilitaryRank - Pay Grade Strings

    /// <summary>
    /// Verifies that pay grade strings (e.g., "E-5", "O-6") are parsed to the correct rank.
    /// </summary>
    [Theory]
    [InlineData("E-1", MilitaryRank.AB)]
    [InlineData("E-5", MilitaryRank.SSgt)]
    [InlineData("E-9", MilitaryRank.CMSgt)]
    [InlineData("O-1", MilitaryRank.SecondLt)]
    [InlineData("O-6", MilitaryRank.Col)]
    [InlineData("O-10", MilitaryRank.Gen)]
    public void ParseMilitaryRank_PayGradeString_ReturnsRank(string input, MilitaryRank expected)
    {
        var result = LineOfDutyCaseMapper.ParseMilitaryRank(input);
        Assert.Equal(expected, result);
    }

    // ParseMilitaryRank - Full Name Strings

    /// <summary>
    /// Verifies that full name strings (e.g., "Staff Sergeant") are parsed to the correct rank.
    /// </summary>
    [Theory]
    [InlineData("STAFF SERGEANT", MilitaryRank.SSgt)]
    [InlineData("LIEUTENANT COLONEL", MilitaryRank.LtCol)]
    [InlineData("BRIGADIER GENERAL", MilitaryRank.BrigGen)]
    [InlineData("AIRMAN FIRST CLASS", MilitaryRank.A1C)]
    public void ParseMilitaryRank_FullNameString_ReturnsRank(string input, MilitaryRank expected)
    {
        var result = LineOfDutyCaseMapper.ParseMilitaryRank(input);
        Assert.Equal(expected, result);
    }

    // ParseMilitaryRank - Multi-Word Abbreviations

    /// <summary>
    /// Verifies that multi-word abbreviations (e.g., "Lt Col", "2d Lt") are parsed correctly.
    /// </summary>
    [Theory]
    [InlineData("Lt Col", MilitaryRank.LtCol)]
    [InlineData("2d Lt", MilitaryRank.SecondLt)]
    [InlineData("1st Lt", MilitaryRank.FirstLt)]
    [InlineData("Brig Gen", MilitaryRank.BrigGen)]
    [InlineData("Maj Gen", MilitaryRank.MajGen)]
    public void ParseMilitaryRank_MultiWordAbbreviation_ReturnsRank(string input, MilitaryRank expected)
    {
        var result = LineOfDutyCaseMapper.ParseMilitaryRank(input);
        Assert.Equal(expected, result);
    }

    // ParseMilitaryRank - Null/Empty/Invalid

    /// <summary>
    /// Verifies that null, empty, and whitespace-only strings return null.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseMilitaryRank_NullOrEmpty_ReturnsNull(string input)
    {
        var result = LineOfDutyCaseMapper.ParseMilitaryRank(input);
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that an unrecognized rank string returns null.
    /// </summary>
    [Fact]
    public void ParseMilitaryRank_InvalidString_ReturnsNull()
    {
        var result = LineOfDutyCaseMapper.ParseMilitaryRank("Admiral");
        Assert.Null(result);
    }

    // ToLineOfDutyViewModel - Name Parsing (comma delimited)

    /// <summary>
    /// Verifies that "Last, First M." format is parsed into separate name components.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_CommaDelimitedName_ParsesNameParts()
    {
        var lodCase = CreateMinimalCase();
        lodCase.MemberName = "Johnson, Marcus A.";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Johnson", vm.LastName);
        Assert.Equal("Marcus", vm.FirstName);
        Assert.Equal("A", vm.MiddleInitial);
    }

    // ToLineOfDutyViewModel - Name Parsing (space delimited)

    /// <summary>
    /// Verifies that "First Last" (no middle initial) format is parsed correctly.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_SpaceDelimitedName_ParsesNameParts()
    {
        var lodCase = CreateMinimalCase();
        lodCase.MemberName = "John Doe";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("John", vm.FirstName);
        Assert.Equal("Doe", vm.LastName);
        Assert.Equal(string.Empty, vm.MiddleInitial);
    }

    // ToLineOfDutyViewModel - Name Parsing (rank prefix)

    /// <summary>
    /// Verifies that rank prefixes are stripped before name parsing.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_RankPrefixedName_StripsRankAndParses()
    {
        var lodCase = CreateMinimalCase();
        lodCase.MemberName = "TSgt Marcus A. Johnson";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Marcus", vm.FirstName);
        Assert.Equal("A", vm.MiddleInitial);
        Assert.Equal("Johnson", vm.LastName);
    }

    // ToLineOfDutyViewModel - Name Parsing (null)

    /// <summary>
    /// Verifies that null or empty MemberName results in empty name parts.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_NullMemberName_EmptyNameParts()
    {
        var lodCase = CreateMinimalCase();
        lodCase.MemberName = null;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal(string.Empty, vm.LastName);
        Assert.Equal(string.Empty, vm.FirstName);
        Assert.Equal(string.Empty, vm.MiddleInitial);
    }

    // ToLineOfDutyViewModel - Name Parsing (single word)

    /// <summary>
    /// Verifies that a single-word name is treated as the last name.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_SingleWordName_TreatedAsLastName()
    {
        var lodCase = CreateMinimalCase();
        lodCase.MemberName = "Smith";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Smith", vm.LastName);
        Assert.Equal(string.Empty, vm.FirstName);
    }

    // ToLineOfDutyViewModel - SSN Mapping

    /// <summary>
    /// Verifies that the ServiceNumber is mapped to the SSN field on the view model.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_ServiceNumber_MapsToSSN()
    {
        var lodCase = CreateMinimalCase();
        lodCase.ServiceNumber = "123-45-6789";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("123-45-6789", vm.SSN);
    }

    // ToLineOfDutyViewModel - Completed status

    /// <summary>
    /// Verifies that a case with a <see cref="LineOfDutyCase.CompletionDate"/> set derives "Completed" status.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_CompletionDateSet_StatusCompleted()
    {
        var lodCase = CreateMinimalCase();
        lodCase.CompletionDate = new DateTime(2025, 6, 1);

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Completed", vm.Status);
    }

    // ToLineOfDutyViewModel - Interim LOD status

    /// <summary>
    /// Verifies that a case with <see cref="LineOfDutyCase.IsInterimLOD"/> set (but no completion date)
    /// derives "Interim LOD" status.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_IsInterimLOD_StatusInterim()
    {
        var lodCase = CreateMinimalCase();
        lodCase.IsInterimLOD = true;
        lodCase.CompletionDate = null;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Interim LOD", vm.Status);
    }

    // ToLineOfDutyViewModel - In Progress status

    /// <summary>
    /// Verifies that a case with no completion date and no interim LOD derives "In Progress" status.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_NoCompletionNoInterim_StatusInProgress()
    {
        var lodCase = CreateMinimalCase();
        lodCase.CompletionDate = null;
        lodCase.IsInterimLOD = false;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("In Progress", vm.Status);
    }

    // ToLineOfDutyViewModel - Completed status takes precedence

    /// <summary>
    /// Verifies that CompletionDate takes precedence over IsInterimLOD for status derivation.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_CompletionDateAndInterim_CompletedWins()
    {
        var lodCase = CreateMinimalCase();
        lodCase.CompletionDate = new DateTime(2025, 6, 1);
        lodCase.IsInterimLOD = true;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Completed", vm.Status);
    }

    // ToLineOfDutyViewModel - Rank mapping

    /// <summary>
    /// Verifies that a valid MemberRank is mapped to the full name and pay grade on the view model.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_ValidRank_MapsFullNameAndGrade()
    {
        var lodCase = CreateMinimalCase();
        lodCase.MemberRank = "TSgt";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Technical Sergeant", vm.Rank);
        Assert.Equal("E-6", vm.Grade);
    }

    // ToLineOfDutyViewModel - Unrecognized rank

    /// <summary>
    /// Verifies that an unrecognizable rank string is preserved as-is with an empty grade.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_UnrecognizedRank_PreservesRawString()
    {
        var lodCase = CreateMinimalCase();
        lodCase.MemberRank = "Unknown Rank";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Unknown Rank", vm.Rank);
        Assert.Equal(string.Empty, vm.Grade);
    }

    // ToLineOfDutyViewModel - Component to MemberStatus AFR

    /// <summary>
    /// Verifies that AirForceReserve component maps to MemberStatus "AFR" on the view model.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_AirForceReserve_MemberStatusAFR()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Component = ServiceComponent.AirForceReserve;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("AFR", vm.MemberStatus);
    }

    // ToLineOfDutyViewModel - Component to MemberStatus ANG

    /// <summary>
    /// Verifies that AirNationalGuard component maps to MemberStatus "ANG" on the view model.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_AirNationalGuard_MemberStatusANG()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Component = ServiceComponent.AirNationalGuard;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("ANG", vm.MemberStatus);
    }

    // ToLineOfDutyViewModel - Component to MemberStatus empty

    /// <summary>
    /// Verifies that RegularAirForce component maps to an empty MemberStatus string
    /// (since RegAF is not an ARC member status).
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_RegularAirForce_EmptyMemberStatus()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Component = ServiceComponent.RegularAirForce;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal(string.Empty, vm.MemberStatus);
    }

    // ToLineOfDutyViewModel - Finding to recommendation mapping

    /// <summary>
    /// Verifies that the FinalFinding is mapped to a CommanderRecommendation on the view model.
    /// </summary>
    [Theory]
    [InlineData(FindingType.InLineOfDuty, CommanderRecommendation.InLineOfDuty)]
    [InlineData(FindingType.NotInLineOfDutyDueToMisconduct, CommanderRecommendation.NotInLineOfDutyDueToMisconduct)]
    [InlineData(FindingType.NotInLineOfDutyNotDueToMisconduct, CommanderRecommendation.NotInLineOfDutyNotDueToMisconduct)]
    public void ToLineOfDutyViewModel_Finding_MapsToRecommendation(FindingType finding, CommanderRecommendation expected)
    {
        var lodCase = CreateMinimalCase();
        lodCase.FinalFinding = finding;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal(expected, vm.Recommendation);
    }

    // ToLineOfDutyViewModel - Unmapped findings

    /// <summary>
    /// Verifies that findings without a direct recommendation mapping (e.g., EPTS) return null.
    /// </summary>
    [Theory]
    [InlineData(FindingType.ExistingPriorToServiceNotAggravated)]
    [InlineData(FindingType.Undetermined)]
    [InlineData(FindingType.EightYearRuleApplied)]
    public void ToLineOfDutyViewModel_UnmappedFinding_RecommendationNull(FindingType finding)
    {
        var lodCase = CreateMinimalCase();
        lodCase.FinalFinding = finding;

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Null(vm.Recommendation);
    }

    // ToLineOfDutyViewModel - Legal sufficiency true

    /// <summary>
    /// Verifies that "Legally sufficient" SJA recommendation maps to IsLegallySufficient = true.
    /// Uses exact equality to prevent the substring match bug where "Legally insufficient" could
    /// also match a Contains("sufficient") check.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_LegallySufficient_MapsTrue()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>
        {
            new() { Role = "Staff Judge Advocate", Recommendation = "Legally sufficient" }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.True(vm.IsLegallySufficient);
    }

    // ToLineOfDutyViewModel - Legal sufficiency false

    /// <summary>
    /// Verifies that "Legally insufficient" SJA recommendation maps to IsLegallySufficient = false.
    /// This test catches the known Contains("sufficient") substring match bug.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_LegallyInsufficient_MapsFalse()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>
        {
            new() { Role = "Staff Judge Advocate", Recommendation = "Legally insufficient" }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.False(vm.IsLegallySufficient);
    }

    // ToLineOfDutyViewModel - No SJA authority

    /// <summary>
    /// Verifies that a missing SJA authority results in IsLegallySufficient = null.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_NoSjaAuthority_IsLegallySufficientNull()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>();

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Null(vm.IsLegallySufficient);
    }

    // ToLineOfDutyViewModel - SJA comments splitting

    /// <summary>
    /// Verifies that SJA comments are split into legal remarks (non-prefixed entries)
    /// and non-concurrence reasons ("Non-concurrence: " prefixed entries).
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_SjaComments_SplitsRemarksAndNonConcurrence()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>
        {
            new()
            {
                Role = "Staff Judge Advocate",
                Recommendation = "Legally sufficient",
                Comments = new List<string>
                {
                    "Case well-documented.",
                    "Non-concurrence: Insufficient evidence for NILOD finding."
                }
            }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Case well-documented.", vm.LegalRemarks);
        Assert.Equal("Insufficient evidence for NILOD finding.", vm.NonConcurrenceReason);
    }

    // ToLineOfDutyViewModel - SJA remarks only

    /// <summary>
    /// Verifies that SJA comments with only standard remarks produce empty non-concurrence reason.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_SjaRemarksOnly_NonConcurrenceEmpty()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>
        {
            new()
            {
                Role = "Staff Judge Advocate",
                Recommendation = "Legally sufficient",
                Comments = new List<string> { "All documentation reviewed." }
            }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("All documentation reviewed.", vm.LegalRemarks);
        Assert.Equal(string.Empty, vm.NonConcurrenceReason);
    }

    // ToLineOfDutyViewModel - Null SJA comments

    /// <summary>
    /// Verifies that null SJA comments produce empty legal remarks and empty non-concurrence reason.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_NullSjaComments_EmptyStrings()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>
        {
            new()
            {
                Role = "Staff Judge Advocate",
                Recommendation = "Legally sufficient",
                Comments = null
            }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal(string.Empty, vm.LegalRemarks);
        Assert.Equal(string.Empty, vm.NonConcurrenceReason);
    }

    // ToLineOfDutyViewModel - Toxicology report present

    /// <summary>
    /// Verifies that a non-empty, non-"Not applicable" toxicology report maps to
    /// ToxicologyTestDone = true with the report text.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_ToxicologyReportPresent_TestDoneTrue()
    {
        var lodCase = CreateMinimalCase();
        lodCase.ToxicologyReport = "Negative for all substances";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.True(vm.ToxicologyTestDone);
        Assert.Equal("Negative for all substances", vm.ToxicologyTestResults);
    }

    // ToLineOfDutyViewModel - Toxicology not applicable

    /// <summary>
    /// Verifies that "Not applicable" toxicology report maps to ToxicologyTestDone = null.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_ToxicologyNotApplicable_TestDoneNull()
    {
        var lodCase = CreateMinimalCase();
        lodCase.ToxicologyReport = "Not applicable";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Null(vm.ToxicologyTestDone);
        Assert.Equal(string.Empty, vm.ToxicologyTestResults);
    }

    // ToLineOfDutyViewModel - Commander authority mapping

    /// <summary>
    /// Verifies that commander authority fields are mapped to the view model.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_CommanderAuthority_MapsFields()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>
        {
            new()
            {
                Role = "Immediate Commander",
                Name = "Smith, Jane",
                Rank = "Col",
                Title = "99 ABW/CC",
                ActionDate = new DateTime(2025, 4, 1),
                Comments = new List<string> { "Concur with ILOD finding." }
            }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Smith, Jane", vm.CommanderName);
        Assert.Equal(MilitaryRank.Col, vm.CommanderRank);
        Assert.Equal("99 ABW/CC", vm.CommanderOrganization);
        Assert.Equal(new DateTime(2025, 4, 1), vm.CommanderSignatureDate);
        Assert.Equal("Concur with ILOD finding.", vm.RecommendationRemarks);
    }

    // ToLineOfDutyViewModel - Authority case-insensitive role match

    /// <summary>
    /// Verifies that authority role matching is case-insensitive.
    /// </summary>
    [Fact]
    public void ToLineOfDutyViewModel_AuthorityRoleCaseInsensitive_Matches()
    {
        var lodCase = CreateMinimalCase();
        lodCase.Authorities = new List<LineOfDutyAuthority>
        {
            new() { Role = "IMMEDIATE COMMANDER", Name = "Test Commander" }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(lodCase);

        Assert.Equal("Test Commander", vm.CommanderName);
    }

    // ApplyToCase - Name assembly with middle initial

    /// <summary>
    /// Verifies that the view model's separate name parts are assembled into MemberName on the case.
    /// </summary>
    [Fact]
    public void ApplyToCase_NameParts_AssemblesFullName()
    {
        var vm = new LineOfDutyViewModel
        {
            FirstName = "Marcus",
            MiddleInitial = "A",
            LastName = "Johnson"
        };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal("Marcus A. Johnson", target.MemberName);
    }

    // ApplyToCase - Name assembly without middle initial

    /// <summary>
    /// Verifies that missing middle initial produces "First Last" without extra spaces.
    /// </summary>
    [Fact]
    public void ApplyToCase_NoMiddleInitial_AssemblesWithoutMiddle()
    {
        var vm = new LineOfDutyViewModel
        {
            FirstName = "John",
            LastName = "Doe"
        };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal("John Doe", target.MemberName);
    }

    // ApplyToCase - MemberStatus AFR to AirForceReserve

    /// <summary>
    /// Verifies that MemberStatus "AFR" on the view model maps to AirForceReserve component.
    /// </summary>
    [Fact]
    public void ApplyToCase_MemberStatusAFR_MapsToAirForceReserve()
    {
        var vm = new LineOfDutyViewModel { MemberStatus = "AFR" };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal(ServiceComponent.AirForceReserve, target.Component);
    }

    // ApplyToCase - MemberStatus ANG to AirNationalGuard

    /// <summary>
    /// Verifies that MemberStatus "ANG" on the view model maps to AirNationalGuard component.
    /// </summary>
    [Fact]
    public void ApplyToCase_MemberStatusANG_MapsToAirNationalGuard()
    {
        var vm = new LineOfDutyViewModel { MemberStatus = "ANG" };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal(ServiceComponent.AirNationalGuard, target.Component);
    }

    // ApplyToCase - Recommendation to Finding mapping

    /// <summary>
    /// Verifies that the view model Recommendation maps to the correct FinalFinding on the case.
    /// </summary>
    [Theory]
    [InlineData(CommanderRecommendation.InLineOfDuty, FindingType.InLineOfDuty)]
    [InlineData(CommanderRecommendation.NotInLineOfDutyDueToMisconduct, FindingType.NotInLineOfDutyDueToMisconduct)]
    [InlineData(CommanderRecommendation.NotInLineOfDutyNotDueToMisconduct, FindingType.NotInLineOfDutyNotDueToMisconduct)]
    public void ApplyToCase_Recommendation_MapsToFinalFinding(CommanderRecommendation rec, FindingType expected)
    {
        var vm = new LineOfDutyViewModel { Recommendation = rec };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal(expected, target.FinalFinding);
    }

    // ApplyToCase - Null recommendation preserves existing

    /// <summary>
    /// Verifies that a null Recommendation preserves the existing FinalFinding on the case.
    /// </summary>
    [Fact]
    public void ApplyToCase_NullRecommendation_PreservesExistingFinding()
    {
        var vm = new LineOfDutyViewModel { Recommendation = null };
        var target = CreateMinimalCase();
        target.FinalFinding = FindingType.NotInLineOfDutyDueToMisconduct;

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal(FindingType.NotInLineOfDutyDueToMisconduct, target.FinalFinding);
    }

    // ApplyToCase - Authority creation

    /// <summary>
    /// Verifies that ApplyToCase creates new authority entries when they do not exist on the case.
    /// </summary>
    [Fact]
    public void ApplyToCase_NoExistingAuthorities_CreatesNewOnes()
    {
        var vm = new LineOfDutyViewModel
        {
            CommanderName = "New Commander",
            CommanderRank = MilitaryRank.Col,
            SJAName = "New SJA",
            SJARank = MilitaryRank.LtCol,
            MedicalProvider = "Dr. Test"
        };
        var target = CreateMinimalCase();
        target.Authorities = new List<LineOfDutyAuthority>();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal(3, target.Authorities.Count);
        Assert.Contains(target.Authorities, a => a.Role == "Immediate Commander" && a.Name == "New Commander");
        Assert.Contains(target.Authorities, a => a.Role == "Staff Judge Advocate" && a.Name == "New SJA");
        Assert.Contains(target.Authorities, a => a.Role == "Medical Provider" && a.Name == "Dr. Test");
    }

    // ApplyToCase - Authority update in place

    /// <summary>
    /// Verifies that ApplyToCase updates existing authority entries rather than creating duplicates.
    /// </summary>
    [Fact]
    public void ApplyToCase_ExistingAuthorities_UpdatesInPlace()
    {
        var vm = new LineOfDutyViewModel
        {
            CommanderName = "Updated Commander",
            CommanderRank = MilitaryRank.BrigGen
        };
        var existingCommander = new LineOfDutyAuthority
        {
            Role = "Immediate Commander",
            Name = "Original Commander",
            Rank = "Col"
        };
        var target = CreateMinimalCase();
        target.Authorities = new List<LineOfDutyAuthority> { existingCommander };

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal("Updated Commander", existingCommander.Name);
        Assert.Equal("BrigGen", existingCommander.Rank);
    }

    // ApplyToCase - IsLegallySufficient true

    /// <summary>
    /// Verifies that IsLegallySufficient true maps to "Legally sufficient" SJA recommendation.
    /// </summary>
    [Fact]
    public void ApplyToCase_IsLegallySufficientTrue_SetsLegallySufficient()
    {
        var vm = new LineOfDutyViewModel { IsLegallySufficient = true };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        var sja = target.Authorities.First(a => a.Role == "Staff Judge Advocate");
        Assert.Equal("Legally sufficient", sja.Recommendation);
    }

    // ApplyToCase - IsLegallySufficient false

    /// <summary>
    /// Verifies that IsLegallySufficient false maps to "Legally insufficient" SJA recommendation.
    /// </summary>
    [Fact]
    public void ApplyToCase_IsLegallySufficientFalse_SetsLegallyInsufficient()
    {
        var vm = new LineOfDutyViewModel { IsLegallySufficient = false };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        var sja = target.Authorities.First(a => a.Role == "Staff Judge Advocate");
        Assert.Equal("Legally insufficient", sja.Recommendation);
    }

    // ApplyToCase - Non-concurrence storage

    /// <summary>
    /// Verifies that non-concurrence with a reason is stored as a prefixed SJA comment.
    /// </summary>
    [Fact]
    public void ApplyToCase_NonConcurrence_StoresAsPrefixedComment()
    {
        var vm = new LineOfDutyViewModel
        {
            ConcurWithRecommendation = false,
            NonConcurrenceReason = "Evidence does not support finding.",
            LegalRemarks = "Reviewed all documents."
        };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        var sja = target.Authorities.First(a => a.Role == "Staff Judge Advocate");
        Assert.Contains("Reviewed all documents.", sja.Comments);
        Assert.Contains("Non-concurrence: Evidence does not support finding.", sja.Comments);
    }

    // ApplyToCase - Toxicology done stores results

    /// <summary>
    /// Verifies that ToxicologyTestDone = true stores the test results on the case.
    /// </summary>
    [Fact]
    public void ApplyToCase_ToxicologyDone_StoresResults()
    {
        var vm = new LineOfDutyViewModel
        {
            ToxicologyTestDone = true,
            ToxicologyTestResults = "Negative"
        };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal("Negative", target.ToxicologyReport);
    }

    // ApplyToCase - Toxicology not done stores Not applicable

    /// <summary>
    /// Verifies that ToxicologyTestDone = false stores "Not applicable" on the case.
    /// </summary>
    [Fact]
    public void ApplyToCase_ToxicologyNotDone_StoresNotApplicable()
    {
        var vm = new LineOfDutyViewModel { ToxicologyTestDone = false };
        var target = CreateMinimalCase();

        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal("Not applicable", target.ToxicologyReport);
    }

    // Round-trip - Basic scalar fields

    /// <summary>
    /// Verifies that basic scalar fields survive a full ToLineOfDutyViewModel then ApplyToCase round-trip.
    /// </summary>
    [Fact]
    public void RoundTrip_BasicFields_PreservedAfterConversion()
    {
        var original = CreateMinimalCase();
        original.MemberName = "Johnson, Marcus A.";
        original.MemberRank = "TSgt";
        original.Unit = "99 SFS/S3";
        original.ServiceNumber = "123456789";
        original.IncidentDescription = "Injured during PT test.";

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(original);
        var target = CreateMinimalCase();
        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        Assert.Equal(original.Unit, target.Unit);
        Assert.Equal(original.ServiceNumber, target.ServiceNumber);
        Assert.Equal(original.IncidentDescription, target.IncidentDescription);
    }

    // Round-trip - SJA comments preserved

    /// <summary>
    /// Verifies that SJA comments survive a round-trip through the view model, correctly
    /// splitting legal remarks and non-concurrence reasons.
    /// </summary>
    [Fact]
    public void RoundTrip_SjaComments_PreservedAfterConversion()
    {
        var original = CreateMinimalCase();
        original.Authorities = new List<LineOfDutyAuthority>
        {
            new()
            {
                Role = "Staff Judge Advocate",
                Name = "Jones, Sarah",
                Rank = "LtCol",
                Recommendation = "Legally sufficient",
                Comments = new List<string>
                {
                    "All evidence reviewed.",
                    "Non-concurrence: Process incomplete."
                }
            }
        };

        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(original);

        // Verify view model split
        Assert.Equal("All evidence reviewed.", vm.LegalRemarks);
        Assert.Equal("Process incomplete.", vm.NonConcurrenceReason);
        Assert.True(vm.IsLegallySufficient);

        // Apply back and verify reassembly
        var target = CreateMinimalCase();
        vm.ConcurWithRecommendation = false; // needed to trigger non-concurrence storage
        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        var sja = target.Authorities.First(a => a.Role == "Staff Judge Advocate");
        Assert.Contains("All evidence reviewed.", sja.Comments);
        Assert.Contains("Non-concurrence: Process incomplete.", sja.Comments);
        Assert.Equal("Legally sufficient", sja.Recommendation);
    }
}
