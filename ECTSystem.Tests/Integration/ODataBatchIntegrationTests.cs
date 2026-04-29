using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Integration;

/// <summary>
/// Verifies the OData JSON-format <c>$batch</c> endpoint executes multiple
/// sub-requests in a single HTTP round-trip. Locks in Rec #2 (eliminate the
/// N+1 in <c>WorkflowHistoryService.AddHistoryEntriesAsync</c>) by exercising
/// the same wire format the client now uses via
/// <c>ODataServiceBase.BatchPostJsonAsync</c>.
/// </summary>
public class ODataBatchIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions BatchJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    public ODataBatchIntegrationTests(EctSystemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Batch_TwoWorkflowHistoryPosts_ExecuteInSingleRequest()
    {
        await AuthenticateAsync();

        var caseDbId = await SeedMinimalCaseAsync();

        var batch = new
        {
            requests = new[]
            {
                new
                {
                    id = "1",
                    method = "POST",
                    url = "/odata/WorkflowStateHistory",
                    headers = new Dictionary<string, string>
                    {
                        ["content-type"] = "application/json",
                        ["accept"] = "application/json"
                    },
                    body = new { LineOfDutyCaseId = caseDbId, WorkflowState = nameof(WorkflowState.Draft) }
                },
                new
                {
                    id = "2",
                    method = "POST",
                    url = "/odata/WorkflowStateHistory",
                    headers = new Dictionary<string, string>
                    {
                        ["content-type"] = "application/json",
                        ["accept"] = "application/json"
                    },
                    body = new { LineOfDutyCaseId = caseDbId, WorkflowState = nameof(WorkflowState.MemberInformationEntry) }
                }
            }
        };

        var response = await Client.PostAsJsonAsync(
            "/odata/$batch",
            batch,
            BatchJsonOptions,
            TestContext.Current.CancellationToken);

        var bodyText = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK from $batch but got {response.StatusCode}: {bodyText}");

        var envelope = JsonSerializer.Deserialize<JsonElement>(bodyText);
        Assert.True(envelope.TryGetProperty("responses", out var responses),
            $"Batch response missing 'responses' property. Body: {bodyText}");

        var subResponses = responses.EnumerateArray().ToList();
        Assert.Equal(2, subResponses.Count);

        foreach (var sub in subResponses)
        {
            var status = sub.GetProperty("status").GetInt32();
            Assert.True(status is >= 200 and < 300,
                $"Sub-request {sub.GetProperty("id").GetString()} failed with status {status}: {sub}");
        }

        // Verify both rows actually persisted via a single batched call
        using var scope = Factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        var saved = await context.WorkflowStateHistories
            .Where(h => h.LineOfDutyCaseId == caseDbId)
            .OrderBy(h => h.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, saved.Count);
        Assert.Equal(WorkflowState.Draft, saved[0].WorkflowState);
        Assert.Equal(WorkflowState.MemberInformationEntry, saved[1].WorkflowState);
    }

    private async Task<int> SeedMinimalCaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        var member = new Member
        {
            FirstName = "Batch",
            LastName = "Tester",
            Rank = "SrA",
            Component = ServiceComponent.AirForceReserve,
            ServiceNumber = "5550000001"
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var lodCase = new LineOfDutyCase
        {
            CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(800, 899)}",
            MemberName = "Tester, Batch",
            MemberRank = "SrA",
            MemberId = member.Id,
            ProcessType = ProcessType.Informal,
            Component = ServiceComponent.AirForceReserve,
            IncidentType = IncidentType.Injury,
            IncidentDate = DateTime.UtcNow.AddDays(-3),
            IncidentDescription = "Batch test seed",
            MEDCON = new MEDCONDetail(),
            INCAP = new INCAPDetails()
        };
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return lodCase.Id;
    }
}
