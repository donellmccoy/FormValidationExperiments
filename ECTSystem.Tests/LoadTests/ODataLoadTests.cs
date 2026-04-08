using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace ECTSystem.Tests.LoadTests;

/// <summary>
/// NBomber load tests targeting OData API endpoints through the in-process
/// <see cref="WebApplicationFactory{TEntryPoint}"/>. These tests validate
/// throughput and latency under concurrent load without requiring an external server.
/// <para>
/// Run selectively: dotnet test --filter "FullyQualifiedName~LoadTests"
/// </para>
/// </summary>
[Trait("Category", "LoadTest")]
public class ODataLoadTests : IClassFixture<Integration.EctSystemWebApplicationFactory>
{
    private readonly Integration.EctSystemWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public ODataLoadTests(Integration.EctSystemWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    /// <summary>
    /// Authenticates and returns a bearer token for use in load test scenarios.
    /// </summary>
    private async Task<string> GetBearerTokenAsync(HttpClient client)
    {
        var loginPayload = new { email = "test@ect.mil", password = "Pass123" };
        var response = await client.PostAsJsonAsync("/login", loginPayload);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString();
    }

    /// <summary>
    /// Seeds <paramref name="count"/> sets of <see cref="Member"/>, <see cref="MEDCONDetail"/>,
    /// and <see cref="INCAPDetails"/> so each POST request can reference unique 1-to-1 FKs.
    /// </summary>
    private async Task<List<(int MemberId, int MedconId, int IncapId)>> SeedRequiredEntitiesAsync(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var results = new List<(int, int, int)>(count);

        for (var i = 0; i < count; i++)
        {
            var member = new Member
            {
                FirstName = "Load",
                LastName = $"Tester{i}",
                Rank = "A1C",
                ServiceNumber = $"000-00-{i:D4}",
                Unit = "TEST/SQ",
                Component = ServiceComponent.RegularAirForce
            };
            context.Members.Add(member);

            var medcon = new MEDCONDetail();
            context.MEDCONDetails.Add(medcon);

            var incap = new INCAPDetails();
            context.INCAPDetails.Add(incap);

            await context.SaveChangesAsync();
            results.Add((member.Id, medcon.Id, incap.Id));
        }

        return results;
    }

    [Fact]
    public async Task GetCases_SustainedLoad()
    {
        using var client = _factory.CreateClient();
        var token = await GetBearerTokenAsync(client);

        var scenario = Scenario.Create("get_cases", async context =>
        {
            var request = Http.CreateRequest("GET", "/odata/Cases?$top=10")
                .WithHeader("Authorization", $"Bearer {token}");

            var response = await Http.Send(client, request);
            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        var scenarioStats = stats.ScenarioStats[0];

        _output.WriteLine($"OK count: {scenarioStats.Ok.Request.Count}");
        _output.WriteLine($"Fail count: {scenarioStats.Fail.Request.Count}");
        _output.WriteLine($"Mean latency: {scenarioStats.Ok.Latency.MeanMs} ms");
        _output.WriteLine($"P95 latency: {scenarioStats.Ok.Latency.Percent95} ms");
        _output.WriteLine($"P99 latency: {scenarioStats.Ok.Latency.Percent99} ms");
        _output.WriteLine($"RPS: {scenarioStats.Ok.Request.RPS}");

        Assert.True(scenarioStats.Fail.Request.Count == 0, $"Expected 0 failures but got {scenarioStats.Fail.Request.Count}");
    }

    [Fact]
    public async Task CreateCase_BurstLoad()
    {
        using var client = _factory.CreateClient();
        var token = await GetBearerTokenAsync(client);
        var ids = await SeedRequiredEntitiesAsync(50);
        var counter = 0;

        var scenario = Scenario.Create("create_case", async context =>
        {
            var sequence = Interlocked.Increment(ref counter);
            var (memberId, medconId, incapId) = ids[(sequence - 1) % ids.Count];
            var payload = new
            {
                MemberName = $"Load, Test {sequence}",
                MemberRank = "A1C",
                ProcessType = "Informal",
                Component = "AirForceReserve",
                IncidentType = "Injury",
                IncidentDutyStatus = "Title10ActiveDuty",
                IncidentDate = DateTime.UtcNow.AddDays(-1),
                InitiationDate = DateTime.UtcNow,
                IncidentDescription = $"Load test case {sequence}",
                MemberId = memberId,
                MEDCONId = medconId,
                INCAPId = incapId
            };

            var json = JsonSerializer.Serialize(payload);

            var request = Http.CreateRequest("POST", "/odata/Cases")
                .WithHeader("Authorization", $"Bearer {token}")
                .WithHeader("Content-Type", "application/json")
                .WithBody(new StringContent(json, Encoding.UTF8, "application/json"));

            var response = await Http.Send(client, request);
            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        var scenarioStats = stats.ScenarioStats[0];

        _output.WriteLine($"OK count: {scenarioStats.Ok.Request.Count}");
        _output.WriteLine($"Fail count: {scenarioStats.Fail.Request.Count}");
        _output.WriteLine($"Mean latency: {scenarioStats.Ok.Latency.MeanMs} ms");
        _output.WriteLine($"P95 latency: {scenarioStats.Ok.Latency.Percent95} ms");

        Assert.True(scenarioStats.Fail.Request.Count == 0, $"Expected 0 failures but got {scenarioStats.Fail.Request.Count}");
    }

    [Fact]
    public async Task MixedReadWrite_ConcurrentLoad()
    {
        using var client = _factory.CreateClient();
        var token = await GetBearerTokenAsync(client);
        var ids = await SeedRequiredEntitiesAsync(50);

        var readScenario = Scenario.Create("read_cases", async context =>
        {
            var request = Http.CreateRequest("GET", "/odata/Cases?$top=5&$orderby=IncidentDate desc")
                .WithHeader("Authorization", $"Bearer {token}");

            var response = await Http.Send(client, request);
            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 15, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        var writeCounter = 0;
        var writeScenario = Scenario.Create("write_cases", async context =>
        {
            var seq = Interlocked.Increment(ref writeCounter);
            var (memberId, medconId, incapId) = ids[(seq - 1) % ids.Count];
            var payload = JsonSerializer.Serialize(new
            {
                MemberName = $"Mixed, Write {seq}",
                MemberRank = "SrA",
                ProcessType = "Informal",
                Component = "RegularAirForce",
                IncidentType = "Illness",
                IncidentDutyStatus = "Title10ActiveDuty",
                IncidentDate = DateTime.UtcNow.AddDays(-2),
                InitiationDate = DateTime.UtcNow,
                IncidentDescription = $"Mixed load test {seq}",
                MemberId = memberId,
                MEDCONId = medconId,
                INCAPId = incapId
            });

            var request = Http.CreateRequest("POST", "/odata/Cases")
                .WithHeader("Authorization", $"Bearer {token}")
                .WithHeader("Content-Type", "application/json")
                .WithBody(new StringContent(payload, Encoding.UTF8, "application/json"));

            var response = await Http.Send(client, request);
            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 3, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        var stats = NBomberRunner
            .RegisterScenarios(readScenario, writeScenario)
            .Run();

        foreach (var scenarioStats in stats.ScenarioStats)
        {
            _output.WriteLine($"--- {scenarioStats.ScenarioName} ---");
            _output.WriteLine($"  OK: {scenarioStats.Ok.Request.Count}, Fail: {scenarioStats.Fail.Request.Count}");
            _output.WriteLine($"  Mean: {scenarioStats.Ok.Latency.MeanMs} ms, P95: {scenarioStats.Ok.Latency.Percent95} ms");
            _output.WriteLine($"  RPS: {scenarioStats.Ok.Request.RPS}");

            Assert.True(scenarioStats.Fail.Request.Count == 0,
                $"Scenario {scenarioStats.ScenarioName}: Expected 0 failures but got {scenarioStats.Fail.Request.Count}");
        }
    }
}
