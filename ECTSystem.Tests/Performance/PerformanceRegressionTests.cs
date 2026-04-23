using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Tests.Integration;
using Xunit;

namespace ECTSystem.Tests.Performance;

/// <summary>
/// Performance regression tests that assert response times stay within acceptable thresholds.
/// Uses <see cref="Stopwatch"/> in xUnit tests against the in-process test server.
/// <para>
/// Run selectively: dotnet test --filter "FullyQualifiedName~Performance"
/// </para>
/// </summary>
[Trait("Category", "Performance")]
public class PerformanceRegressionTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public PerformanceRegressionTests(EctSystemWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task GetCases_ShouldRespondWithin500ms()
    {
        await AuthenticateAsync();

        // Warmup
        await Client.GetAsync("/odata/Cases?$top=1", TestContext.Current.CancellationToken);

        var sw = Stopwatch.StartNew();
        var response = await Client.GetAsync("/odata/Cases?$top=10", TestContext.Current.CancellationToken);
        sw.Stop();

        response.EnsureSuccessStatusCode();
        _output.WriteLine($"GET /odata/Cases: {sw.ElapsedMilliseconds} ms");

        Assert.True(sw.ElapsedMilliseconds < 500,
            $"GET /odata/Cases took {sw.ElapsedMilliseconds} ms, expected < 500 ms");
    }

    [Fact]
    public async Task CreateCase_ShouldRespondWithin1000ms()
    {
        await AuthenticateAsync();

        var payload = JsonSerializer.Serialize(new
        {
            MemberName = "Perf, Test A",
            MemberRank = "TSgt",
            ProcessType = "Informal",
            Component = "RegularAirForce",
            IncidentType = "Injury",
            IncidentDutyStatus = "Title10ActiveDuty",
            IncidentDate = DateTime.UtcNow.AddDays(-1),
            InitiationDate = DateTime.UtcNow,
            IncidentDescription = "Performance regression test case"
        });

        // Warmup
        await Client.PostAsync("/odata/Cases",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        var freshPayload = JsonSerializer.Serialize(new
        {
            MemberName = "Perf, Test B",
            MemberRank = "SSgt",
            ProcessType = "Informal",
            Component = "AirForceReserve",
            IncidentType = "Illness",
            IncidentDutyStatus = "Title10ActiveDuty",
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            InitiationDate = DateTime.UtcNow,
            IncidentDescription = "Performance regression test case 2"
        });

        var sw = Stopwatch.StartNew();
        var response = await Client.PostAsync("/odata/Cases",
            new StringContent(freshPayload, Encoding.UTF8, "application/json"));
        sw.Stop();

        _output.WriteLine($"POST /odata/Cases: {sw.ElapsedMilliseconds} ms (Status: {response.StatusCode})");

        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"POST /odata/Cases took {sw.ElapsedMilliseconds} ms, expected < 1000 ms");
    }

    [Fact]
    public async Task ODataFilter_ShouldRespondWithin500ms()
    {
        await AuthenticateAsync();

        // Warmup
        await Client.GetAsync("/odata/Cases?$filter=Component eq 'RegularAirForce'&$top=5", TestContext.Current.CancellationToken);

        var sw = Stopwatch.StartNew();
        var response = await Client.GetAsync(
            "/odata/Cases?$filter=Component eq 'RegularAirForce'&$top=10&$orderby=IncidentDate desc&$select=Id,CaseId,MemberName,IncidentDate");
        sw.Stop();

        response.EnsureSuccessStatusCode();
        _output.WriteLine($"GET /odata/Cases (filtered): {sw.ElapsedMilliseconds} ms");

        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Filtered GET took {sw.ElapsedMilliseconds} ms, expected < 500 ms");
    }

    [Fact]
    public async Task ODataExpand_ShouldRespondWithin750ms()
    {
        await AuthenticateAsync();

        // Seed a case first
        var seedPayload = JsonSerializer.Serialize(new
        {
            MemberName = "Expand, Test A",
            MemberRank = "A1C",
            ProcessType = "Informal",
            Component = "RegularAirForce",
            IncidentType = "Injury",
            IncidentDutyStatus = "Title10ActiveDuty",
            IncidentDate = DateTime.UtcNow.AddDays(-3),
            InitiationDate = DateTime.UtcNow,
            IncidentDescription = "Expand perf test"
        });
        await Client.PostAsync("/odata/Cases",
            new StringContent(seedPayload, Encoding.UTF8, "application/json"));

        // Warmup
        await Client.GetAsync("/odata/Cases?$expand=Authorities,Documents,WorkflowStateHistories&$top=1", TestContext.Current.CancellationToken);

        var sw = Stopwatch.StartNew();
        var response = await Client.GetAsync(
            "/odata/Cases?$expand=Authorities,Documents,WorkflowStateHistories&$top=5");
        sw.Stop();

        response.EnsureSuccessStatusCode();
        _output.WriteLine($"GET /odata/Cases ($expand): {sw.ElapsedMilliseconds} ms");

        Assert.True(sw.ElapsedMilliseconds < 750,
            $"Expanded GET took {sw.ElapsedMilliseconds} ms, expected < 750 ms");
    }

    [Fact]
    public void MapperRoundTrip_ShouldCompleteWithin5ms()
    {
        var source = new LineOfDutyCase
        {
            Id = 1,
            CaseId = "20250315-001",
            MemberName = "Perf, Mapper A.",
            MemberRank = "TSgt",
            ServiceNumber = "123-45-6789",
            Unit = "99 SFS/S3",
            IncidentDate = new DateTime(2025, 3, 15),
            IncidentDutyStatus = DutyStatus.Title10ActiveDuty,
            Component = ServiceComponent.RegularAirForce,
            IncidentType = IncidentType.Injury,
            ProcessType = ProcessType.Informal,
            IncidentDescription = "PT injury",
            FinalFinding = FindingType.InLineOfDuty,
            Authorities = new List<LineOfDutyAuthority>
            {
                new() { Role = "Immediate Commander", Name = "Maj Test" },
                new() { Role = "Medical Provider", Name = "Col Test" },
                new() { Role = "Staff Judge Advocate", Name = "Lt Col Test", Recommendation = "Legally sufficient" }
            },
            WorkflowStateHistories = new List<WorkflowStateHistory>
            {
                new() { WorkflowState = WorkflowState.Draft, EnteredDate = DateTime.UtcNow }
            }
        };

        // Warmup
        var vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(source);
        var target = new LineOfDutyCase { Authorities = new List<LineOfDutyAuthority>() };
        LineOfDutyCaseMapper.ApplyToCase(vm, target);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            vm = LineOfDutyCaseMapper.ToLineOfDutyViewModel(source);
            target = new LineOfDutyCase { Authorities = new List<LineOfDutyAuthority>() };
            LineOfDutyCaseMapper.ApplyToCase(vm, target);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / 100;
        _output.WriteLine($"Mapper round-trip avg: {avgMs:F3} ms");

        Assert.True(avgMs < 5,
            $"Mapper round-trip averaged {avgMs:F3} ms, expected < 5 ms");
    }

    [Fact]
    public async Task Authentication_ShouldRespondWithin500ms()
    {
        // Measure login time
        var loginPayload = new { email = "test@ect.mil", password = "Pass123" };

        // Warmup
        await Client.PostAsJsonAsync("/login", loginPayload, TestContext.Current.CancellationToken);

        var sw = Stopwatch.StartNew();
        var response = await Client.PostAsJsonAsync("/login", loginPayload, TestContext.Current.CancellationToken);
        sw.Stop();

        response.EnsureSuccessStatusCode();
        _output.WriteLine($"POST /login: {sw.ElapsedMilliseconds} ms");

        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Login took {sw.ElapsedMilliseconds} ms, expected < 500 ms");
    }
}
