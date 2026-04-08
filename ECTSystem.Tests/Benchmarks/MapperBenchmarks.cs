using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Tests.Benchmarks;

/// <summary>
/// Microbenchmarks for <see cref="LineOfDutyCaseMapper"/> conversion methods.
/// Run from command line: dotnet run -c Release --project ECTSystem.Tests -- --filter *MapperBenchmarks*
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class MapperBenchmarks
{
    private LineOfDutyCase _case;
    private LineOfDutyViewModel _viewModel;

    [GlobalSetup]
    public void Setup()
    {
        _case = new LineOfDutyCase
        {
            Id = 1,
            CaseId = "20250315-001",
            MemberName = "Doe, John A.",
            MemberRank = "TSgt",
            ServiceNumber = "123-45-6789",
            Unit = "99 SFS/S3",
            IncidentDate = new DateTime(2025, 3, 15),
            IncidentDutyStatus = DutyStatus.Title10ActiveDuty,
            Component = ServiceComponent.RegularAirForce,
            IncidentType = IncidentType.Injury,
            ProcessType = ProcessType.Informal,
            IncidentDescription = "Sustained a knee injury during organized physical training. Member was performing sprint drills when they heard a pop in their left knee.",
            FinalFinding = FindingType.InLineOfDuty,
            MedicalFindings = "ACL tear, left knee",
            ClinicalDiagnosis = "Complete ACL rupture, anterior cruciate ligament, left knee",
            WasUnderInfluence = false,
            WasMentallyResponsible = true,
            IsPriorServiceCondition = false,
            SjaConcurs = true,
            WingCcSignature = "Col Smith, John D",
            MemberStatementReviewed = true,
            MedicalRecordsReviewed = true,
            Authorities = new List<LineOfDutyAuthority>
            {
                new()
                {
                    Id = 1,
                    Role = "Immediate Commander",
                    Name = "Maj Williams, Robert J",
                    Rank = "Maj",
                    Title = "99 SFS/CC",
                    ActionDate = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = 2,
                    Role = "Medical Provider",
                    Name = "Col Adams, Sarah M",
                    Rank = "Col",
                    Title = "99 MDG/SGH",
                    ActionDate = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = 3,
                    Role = "Staff Judge Advocate",
                    Name = "Lt Col Baker, Thomas R",
                    Rank = "Lt Col",
                    Title = "99 ABW/JA",
                    Recommendation = "Legally sufficient",
                    ActionDate = DateTime.UtcNow.AddDays(-3)
                }
            },
            WorkflowStateHistories = new List<WorkflowStateHistory>
            {
                new()
                {
                    Id = 1,
                    WorkflowState = WorkflowState.Draft,
                    EnteredDate = DateTime.UtcNow.AddDays(-14),
                    ExitDate = DateTime.UtcNow.AddDays(-13)
                },
                new()
                {
                    Id = 2,
                    WorkflowState = WorkflowState.MemberInformationEntry,
                    EnteredDate = DateTime.UtcNow.AddDays(-13),
                    ExitDate = DateTime.UtcNow.AddDays(-10)
                },
                new()
                {
                    Id = 3,
                    WorkflowState = WorkflowState.UnitCommanderReview,
                    EnteredDate = DateTime.UtcNow.AddDays(-10),
                    ExitDate = null
                }
            }
        };

        _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_case);
    }

    [Benchmark(Baseline = true)]
    public LineOfDutyViewModel ToViewModel()
    {
        return LineOfDutyCaseMapper.ToLineOfDutyViewModel(_case);
    }

    [Benchmark]
    public void ApplyToCase()
    {
        var target = new LineOfDutyCase
        {
            Authorities = new List<LineOfDutyAuthority>
            {
                new() { Role = "Immediate Commander" },
                new() { Role = "Medical Provider" },
                new() { Role = "Staff Judge Advocate" }
            }
        };
        LineOfDutyCaseMapper.ApplyToCase(_viewModel, target);
    }

    [Benchmark]
    public MilitaryRank? ParseRank_Enlisted()
    {
        return LineOfDutyCaseMapper.ParseMilitaryRank("TSgt");
    }

    [Benchmark]
    public MilitaryRank? ParseRank_Officer()
    {
        return LineOfDutyCaseMapper.ParseMilitaryRank("Lt Col");
    }

    [Benchmark]
    public string FormatRankFullName()
    {
        return LineOfDutyCaseMapper.FormatRankToFullName(MilitaryRank.TSgt);
    }

    [Benchmark]
    public string FormatRankPayGrade()
    {
        return LineOfDutyCaseMapper.FormatRankToPayGrade(MilitaryRank.TSgt);
    }
}
