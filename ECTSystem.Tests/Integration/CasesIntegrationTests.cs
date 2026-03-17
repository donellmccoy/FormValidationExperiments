using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Integration;

public class CasesIntegrationTests : IntegrationTestBase
{
    private static int _seedCounter;

    public CasesIntegrationTests(EctSystemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Post_CreateCase_GeneratesCaseId()
    {
        await AuthenticateAsync();

        // Seed a member so the case has a valid MemberId FK
        var memberId = await SeedMemberAsync();

        var payload = new
        {
            MemberName = "Doe, John A",
            MemberRank = "TSgt",
            MemberId = memberId,
            ProcessType = nameof(ProcessType.Informal),
            Component = nameof(ServiceComponent.AirForceReserve),
            IncidentType = nameof(IncidentType.Injury),
            IncidentDutyStatus = nameof(DutyStatus.Title10ActiveDuty),
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            InitiationDate = DateTime.UtcNow,
            IncidentDescription = "Training injury during PT"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/odata/Cases")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
        var response = await Client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but got {response.StatusCode}: {responseBody}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var caseId = body.GetProperty("CaseId").GetString();

        Assert.NotNull(caseId);
        // CaseId format: YYYYMMDD-001
        Assert.Matches(@"^\d{8}-\d{3}$", caseId);
    }

    [Fact]
    public async Task Patch_UpdateCase_AppliesFieldChanges()
    {
        await AuthenticateAsync();

        var (caseDbId, rowVersion) = await SeedCaseAsync();

        var patchPayload = new
        {
            IncidentDescription = "Updated: slip and fall during field exercise",
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/odata/Cases({caseDbId})")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(patchPayload),
                Encoding.UTF8,
                "application/json")
        };

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var updatedDescription = body.GetProperty("IncidentDescription").GetString();

        Assert.Equal("Updated: slip and fall during field exercise", updatedDescription);
    }

    [Fact]
    public async Task Upload_And_Download_Document_RoundTrips()
    {
        await AuthenticateAsync();

        var (caseDbId, _) = await SeedCaseAsync();

        // Upload a .txt file (no magic-byte validation for .txt)
        var fileContent = "This is test document content for LOD case."u8.ToArray();
        using var multipart = new MultipartFormDataContent();
        var fileStream = new ByteArrayContent(fileContent);
        fileStream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        multipart.Add(fileStream, "file", "test-document.txt");
        multipart.Add(new StringContent("Supporting Document"), "documentType");
        multipart.Add(new StringContent("Test description"), "description");

        var uploadResponse = await Client.PostAsync($"/api/cases/{caseDbId}/documents", multipart);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var docs = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var docId = docs[0].GetProperty("Id").GetInt32();

        // Download the same document
        var downloadResponse = await Client.GetAsync($"/api/cases/{caseDbId}/documents/{docId}/download");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();

        Assert.Equal(fileContent, downloadedBytes);
    }

    [Fact]
    public async Task Checkout_SecondCheckout_ReturnsConflict()
    {
        await AuthenticateAsync();

        var (caseDbId, _) = await SeedCaseAsync();

        // First checkout should succeed
        var firstResponse = await Client.PostAsync($"/odata/Cases({caseDbId})/Checkout", null);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);

        // Second checkout should be rejected
        var secondResponse = await Client.PostAsync($"/odata/Cases({caseDbId})/Checkout", null);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    /// <summary>
    /// Seeds a <see cref="Member"/> directly in the database and returns its Id.
    /// </summary>
    private async Task<int> SeedMemberAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var member = new Member
        {
            FirstName = "John",
            LastName = "Doe",
            Rank = "TSgt",
            Component = ServiceComponent.AirForceReserve,
            ServiceNumber = "1234567890"
        };

        context.Members.Add(member);
        await context.SaveChangesAsync();

        return member.Id;
    }

    /// <summary>
    /// Seeds a <see cref="LineOfDutyCase"/> with required related entities and returns its database Id and RowVersion.
    /// </summary>
    private async Task<(int Id, byte[] RowVersion)> SeedCaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var member = new Member
        {
            FirstName = "Jane",
            LastName = "Smith",
            Rank = "SSgt",
            Component = ServiceComponent.AirNationalGuard,
            ServiceNumber = "9876543210"
        };

        context.Members.Add(member);
        await context.SaveChangesAsync();

        var suffix = Interlocked.Increment(ref _seedCounter);

        var lodCase = new LineOfDutyCase
        {
            CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{900 + suffix}",
            MemberName = "Smith, Jane B",
            MemberRank = "SSgt",
            MemberId = member.Id,
            ProcessType = ProcessType.Informal,
            Component = ServiceComponent.AirNationalGuard,
            IncidentType = IncidentType.Injury,
            IncidentDate = DateTime.UtcNow.AddDays(-10),
            IncidentDescription = "Initial description"
        };

        context.Cases.Add(lodCase);
        await context.SaveChangesAsync();

        return (lodCase.Id, lodCase.RowVersion);
    }
}
