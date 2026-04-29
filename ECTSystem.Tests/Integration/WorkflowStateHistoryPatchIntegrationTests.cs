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
/// Reproduces the production bug where the Web client's
/// <c>WorkflowHistoryService.UpdateHistoryEndDateAsync</c> PATCH request
/// returns 400 Bad Request from the OData controller.
/// </summary>
public class WorkflowStateHistoryPatchIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions ODataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    public WorkflowStateHistoryPatchIntegrationTests(EctSystemWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("MinValueUtc", "{\"ExitDate\":\"0001-01-01T00:00:00Z\"}")]
    [InlineData("UtcNowZ", "{\"ExitDate\":\"2024-06-01T12:34:56Z\"}")]
    public async Task Patch_AcceptsDateTimesWithUtcDesignator(string label, string rawBody)
    {
        await AuthenticateAsync();

        var caseDbId = await SeedMinimalCaseAsync();
        var postBody = new { LineOfDutyCaseId = caseDbId, WorkflowState = nameof(WorkflowState.Draft) };
        var postResponse = await Client.PostAsJsonAsync("/odata/WorkflowStateHistory", postBody, ODataJsonOptions, TestContext.Current.CancellationToken);
        var postText = await postResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(postResponse.IsSuccessStatusCode, $"POST failed: {(int)postResponse.StatusCode} {postText}");
        var historyId = JsonSerializer.Deserialize<JsonElement>(postText).GetProperty("Id").GetInt32();

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/odata/WorkflowStateHistory({historyId})")
        {
            Content = new StringContent(rawBody, System.Text.Encoding.UTF8, "application/json")
        };
        var patchResponse = await Client.SendAsync(patchRequest, TestContext.Current.CancellationToken);
        var patchText = await patchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(patchResponse.IsSuccessStatusCode,
            $"[{label}] PATCH expected success but got {(int)patchResponse.StatusCode}: {patchText}");
    }

    /// <summary>
    /// Regression: ASP.NET Core OData rejects DateTime strings without a
    /// timezone designator on Edm.DateTimeOffset properties with 400
    /// "The input was not valid.". Documented here so the constraint is
    /// explicit for future client-side PATCH bodies.
    /// </summary>
    [Fact]
    public async Task Patch_WithDateTimeMissingTimezone_Returns400()
    {
        await AuthenticateAsync();

        var caseDbId = await SeedMinimalCaseAsync();
        var postBody = new { LineOfDutyCaseId = caseDbId, WorkflowState = nameof(WorkflowState.Draft) };
        var postResponse = await Client.PostAsJsonAsync("/odata/WorkflowStateHistory", postBody, ODataJsonOptions, TestContext.Current.CancellationToken);
        var historyId = JsonSerializer.Deserialize<JsonElement>(
            await postResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).GetProperty("Id").GetInt32();

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/odata/WorkflowStateHistory({historyId})")
        {
            // No 'Z' / no offset — what System.Text.Json emits for DateTime.MinValue (Kind=Unspecified).
            Content = new StringContent("{\"ExitDate\":\"0001-01-01T00:00:00\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var patchResponse = await Client.SendAsync(patchRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    [Fact]
    public async Task Patch_WithExitDateMinValue_AsClientSends_ReturnsSuccess()
    {
        await AuthenticateAsync();

        var caseDbId = await SeedMinimalCaseAsync();

        // Step 1: POST a history row the same way the client does.
        var postBody = new { LineOfDutyCaseId = caseDbId, WorkflowState = nameof(WorkflowState.Draft) };
        var postResponse = await Client.PostAsJsonAsync("/odata/WorkflowStateHistory", postBody, ODataJsonOptions, TestContext.Current.CancellationToken);
        var postText = await postResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(postResponse.IsSuccessStatusCode, $"POST failed: {(int)postResponse.StatusCode} {postText}");

        var posted = JsonSerializer.Deserialize<JsonElement>(postText);
        var historyId = posted.GetProperty("Id").GetInt32();

        // Step 2: PATCH with the exact same payload shape WorkflowHistoryService.UpdateHistoryEndDateAsync sends.
        var patchBody = new { ExitDate = (DateTime?)DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc) };
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/odata/WorkflowStateHistory({historyId})")
        {
            Content = JsonContent.Create(patchBody, options: ODataJsonOptions)
        };

        var patchResponse = await Client.SendAsync(patchRequest, TestContext.Current.CancellationToken);
        var patchText = await patchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(patchResponse.IsSuccessStatusCode,
            $"PATCH failed: {(int)patchResponse.StatusCode} {patchResponse.ReasonPhrase}\nBody: {patchText}");
    }

    private async Task<int> SeedMinimalCaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        var member = new Member
        {
            FirstName = "Patch",
            LastName = "Repro",
            Rank = "SrA",
            Component = ServiceComponent.AirForceReserve,
            ServiceNumber = "5550000777"
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var lodCase = new LineOfDutyCase
        {
            CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(900, 999)}",
            MemberName = "Repro, Patch",
            MemberRank = "SrA",
            MemberId = member.Id,
            ProcessType = ProcessType.Informal,
            Component = ServiceComponent.AirForceReserve,
            IncidentType = IncidentType.Injury,
            IncidentDate = DateTime.UtcNow.AddDays(-3),
            IncidentDescription = "Patch repro seed",
            MEDCON = new MEDCONDetail(),
            INCAP = new INCAPDetails()
        };
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return lodCase.Id;
    }
}
