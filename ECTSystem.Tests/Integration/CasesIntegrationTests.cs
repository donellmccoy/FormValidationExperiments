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

        // Seed a member and required related entities so the case has valid FKs
        var (memberId, medconId, incapId) = await SeedMemberWithBenefitsAsync();

        var payload = new
        {
            MemberName = "Doe, John A",
            MemberRank = "TSgt",
            ServiceNumber = "1234567890",
            MemberId = memberId,
            MEDCONId = medconId,
            INCAPId = incapId,
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
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but got {response.StatusCode}: {responseBody}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var caseId = body.GetProperty("CaseId").GetString();

        Assert.NotNull(caseId);
        // CaseId format: YYYYMMDD-001
        Assert.Matches(@"^\d{8}-\d{3}$", caseId);
    }

    [Fact]
    public async Task Patch_UpdateCase_AppliesFieldChanges()
    {
        await AuthenticateAsync();

        var (caseDbId, rowVersion, memberId) = await SeedCaseAsync();

        var patchPayload = BuildUpdateCaseDtoPayload("Updated: slip and fall during field exercise", memberId);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/odata/Cases({caseDbId})")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(patchPayload),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("Prefer", "return=representation");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Convert.ToBase64String(rowVersion)}\"");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {response.StatusCode}: {responseBody}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var updatedDescription = body.GetProperty("IncidentDescription").GetString();

        Assert.Equal("Updated: slip and fall during field exercise", updatedDescription);
    }

    [Fact]
    public async Task Upload_And_Download_Document_RoundTrips()
    {
        await AuthenticateAsync();

        var (caseDbId, _, _) = await SeedCaseAsync();

        // Upload a .txt file (no magic-byte validation for .txt)
        var fileContent = "This is test document content for LOD case."u8.ToArray();
        using var multipart = new MultipartFormDataContent();
        var fileStream = new ByteArrayContent(fileContent);
        fileStream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        multipart.Add(fileStream, "file", "test-document.txt");
        multipart.Add(new StringContent("Supporting Document"), "documentType");
        multipart.Add(new StringContent("Test description"), "description");

        var uploadResponse = await Client.PostAsync($"/odata/Cases({caseDbId})/Documents", multipart, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var docsJson = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var docsArray = docsJson.ValueKind == JsonValueKind.Array ? docsJson : docsJson.GetProperty("value");
        var docId = docsArray[0].GetProperty("Id").GetInt32();

        // Download the same document
        var downloadResponse = await Client.GetAsync($"/odata/Documents({docId})/$value", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(fileContent, downloadedBytes);
    }

    [Fact]
    public async Task Patch_WhenCheckedOutByAnotherUser_Returns403()
    {
        // Use a non-Admin user — Admin would bypass the checkout guard.
        await AuthenticateAsMemberAsync();

        var (caseDbId, rowVersion, memberId) = await SeedCaseAsync(checkedOutBy: "00000000-0000-0000-0000-0000000000ff", checkedOutByName: "Other User");

        var patchPayload = BuildUpdateCaseDtoPayload("Should not be applied", memberId);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/odata/Cases({caseDbId})")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(patchPayload),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Convert.ToBase64String(rowVersion)}\"");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected Forbidden but got {response.StatusCode}: {responseBody}");
    }

    [Fact]
    public async Task Get_WithIfNoneMatchWildcard_Returns304()
    {
        await AuthenticateAsync();

        var (caseDbId, _, _) = await SeedCaseAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/odata/Cases({caseDbId})");
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithMatchingIfNoneMatchInList_Returns304()
    {
        await AuthenticateAsync();

        var (caseDbId, rowVersion, _) = await SeedCaseAsync();
        var etag = $"\"{Convert.ToBase64String(rowVersion)}\"";

        var request = new HttpRequestMessage(HttpMethod.Get, $"/odata/Cases({caseDbId})");
        // Comma-separated list with a non-matching tag and the matching tag — RFC 7232 form
        request.Headers.TryAddWithoutValidation("If-None-Match", $"\"deadbeef\", {etag}");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithNonMatchingIfNoneMatch_ReturnsBody()
    {
        await AuthenticateAsync();

        var (caseDbId, _, _) = await SeedCaseAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/odata/Cases({caseDbId})");
        request.Headers.TryAddWithoutValidation("If-None-Match", "\"not-the-current-etag\"");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_SecondCheckout_ReturnsConflict()
    {
        await AuthenticateAsync();

        var (caseDbId, _, _) = await SeedCaseAsync();

        // Read the current RowVersion so we can supply the required If-Match header.
        var getResponse = await Client.GetAsync($"/odata/Cases({caseDbId})?$select=RowVersion", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var caseJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var rowVersion = caseJson.GetProperty("RowVersion").GetBytesFromBase64();
        var ifMatch = new System.Net.Http.Headers.EntityTagHeaderValue($"\"{Convert.ToBase64String(rowVersion)}\"");

        // First checkout should succeed
        var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/odata/Cases({caseDbId})/Checkout");
        firstRequest.Headers.IfMatch.Add(ifMatch);
        var firstResponse = await Client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Second checkout should be rejected. The server validates "already checked out"
        // before the concurrency check, so the same If-Match token still satisfies the
        // precondition and the failure is the 409 we want to assert on.
        var secondRequest = new HttpRequestMessage(HttpMethod.Post, $"/odata/Cases({caseDbId})/Checkout");
        secondRequest.Headers.IfMatch.Add(ifMatch);
        var secondResponse = await Client.SendAsync(secondRequest, TestContext.Current.CancellationToken);
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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member.Id;
    }

    /// <summary>
    /// Seeds a <see cref="Member"/> with required <see cref="MEDCONDetail"/> and <see cref="INCAPDetails"/>
    /// entities and returns their Ids for use in API payloads.
    /// </summary>
    private async Task<(int MemberId, int MEDCONId, int INCAPId)> SeedMemberWithBenefitsAsync()
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

        var medcon = new MEDCONDetail();
        var incap = new INCAPDetails();

        context.Members.Add(member);
        context.MEDCONDetails.Add(medcon);
        context.INCAPDetails.Add(incap);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (member.Id, medcon.Id, incap.Id);
    }

    /// <summary>
    /// Builds an <see cref="UpdateCaseDto"/>-shaped payload satisfying all <c>[Required]</c>
    /// fields, with the supplied <paramref name="incidentDescription"/>.
    /// </summary>
    private static object BuildUpdateCaseDtoPayload(string incidentDescription, int memberId) => new
    {
        ProcessType = nameof(ProcessType.Informal),
        Component = nameof(ServiceComponent.AirNationalGuard),
        MemberName = "Smith, Jane B",
        MemberRank = "SSgt",
        ServiceNumber = "9876543210",
        IncidentType = nameof(IncidentType.Injury),
        IncidentDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-10), DateTimeKind.Utc),
        IncidentDescription = incidentDescription,
        IncidentDutyStatus = nameof(DutyStatus.Title10ActiveDuty),
        MemberId = memberId
    };

    /// <summary>
    /// Seeds a <see cref="LineOfDutyCase"/> with required related entities and returns its database Id and RowVersion.
    /// </summary>
    private async Task<(int Id, byte[] RowVersion, int MemberId)> SeedCaseAsync(string? checkedOutBy = null, string? checkedOutByName = null)
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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var suffix = Interlocked.Increment(ref _seedCounter);

        // Append a short random hex segment to guarantee uniqueness across parallel
        // test classes that may share this static counter or insert into the same
        // SQLite shared in-memory database.
        var unique = Guid.NewGuid().ToString("N")[..6];

        var lodCase = new LineOfDutyCase
        {
            CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{900 + suffix}-{unique}",
            MemberName = "Smith, Jane B",
            MemberRank = "SSgt",
            MemberId = member.Id,
            ProcessType = ProcessType.Informal,
            Component = ServiceComponent.AirNationalGuard,
            IncidentType = IncidentType.Injury,
            IncidentDate = DateTime.UtcNow.AddDays(-10),
            IncidentDescription = "Initial description",
            MEDCON = new MEDCONDetail(),
            INCAP = new INCAPDetails(),
            IsCheckedOut = checkedOutBy is not null,
            CheckedOutBy = checkedOutBy ?? string.Empty,
            CheckedOutByName = checkedOutByName ?? string.Empty,
            CheckedOutDate = checkedOutBy is not null ? DateTime.UtcNow : null
        };

        context.Cases.Add(lodCase);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // SQLite does not auto-populate the SQL Server `rowversion` column. EF maps
        // RowVersion as ValueGeneratedOnAddOrUpdate, so client-set values on the
        // entity are silently ignored. Patch RowVersion via raw SQL so the
        // controller's RowVersion-based existence (Get) and concurrency (Patch/Checkout)
        // checks behave correctly in tests.
        var rowVersionBytes = Guid.NewGuid().ToByteArray();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Cases SET RowVersion = {0} WHERE Id = {1}",
            [rowVersionBytes, lodCase.Id],
            TestContext.Current.CancellationToken);

        return (lodCase.Id, rowVersionBytes, member.Id);
    }
}
