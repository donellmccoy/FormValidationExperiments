using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Shared.Models;
using ECTSystem.Tests.TestData;
using Xunit;

namespace ECTSystem.Tests;

public class DataServiceCaseTests : DataServiceTestBase
{
    // ──────────────────────────── GetCasesQueryable ────────────────────────────

    [Fact]
    public async Task GetCasesQueryable_WhenCasesExist_ReturnsAllCases()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.AddRange(BuildCase(1, "CASE-0001"), BuildCase(2, "CASE-0002"));
        await ctx.SaveChangesAsync();

        var queryable = Sut.GetCasesQueryable();
        var result = await queryable.ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetCasesQueryable_WhenNoCasesExist_ReturnsEmptyList()
    {
        var queryable = Sut.GetCasesQueryable();
        var result = await queryable.ToListAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCasesQueryable_CallsCreateDbContext()
    {
        var _ = Sut.GetCasesQueryable();

        MockFactory.Verify(f => f.CreateDbContext(), Times.Once);
    }

    // ──────────────────────────── GetCaseByKeyAsync ────────────────────────────

    [Theory]
    [ClassData(typeof(CaseKeyExistsTestData))]
    public async Task GetCaseByKeyAsync_ReturnsExpectedResult(int key, bool seedCase)
    {
        if (seedCase)
        {
            await using var ctx = CreateSeedContext();
            ctx.Cases.Add(BuildCase(key));
            await ctx.SaveChangesAsync();
        }

        var result = await Sut.GetCaseByKeyAsync(key);

        if (seedCase)
        {
            Assert.NotNull(result);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task GetCaseByKeyAsync_WhenCaseExists_ReturnsCaseWithCorrectId()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var result = await Sut.GetCaseByKeyAsync(1);

        Assert.Equal(1, result.Id);
        Assert.Equal("SSgt John Doe", result.MemberName);
    }

    [Fact]
    public async Task GetCaseByKeyAsync_WhenCaseExists_IncludesDocuments()
    {
        await using var ctx = CreateSeedContext();
        var lodCase = BuildCase(1);
        lodCase.Documents.Add(new LineOfDutyDocument { FileName = "af348.pdf", ContentType = "application/pdf" });
        ctx.Cases.Add(lodCase);
        await ctx.SaveChangesAsync();

        var result = await Sut.GetCaseByKeyAsync(1);

        Assert.NotNull(result.Documents);
        Assert.Single(result.Documents);
    }

    [Fact]
    public async Task GetCaseByKeyAsync_WhenCaseExists_IncludesAuthorities()
    {
        await using var ctx = CreateSeedContext();
        var lodCase = BuildCase(1);
        lodCase.Authorities.Add(new LineOfDutyAuthority { Role = "Commander", Name = "Col Smith" });
        ctx.Cases.Add(lodCase);
        await ctx.SaveChangesAsync();

        var result = await Sut.GetCaseByKeyAsync(1);

        Assert.Single(result.Authorities);
        Assert.Equal("Col Smith", result.Authorities.First().Name);
    }

    // ──────────────────────────── CreateCaseAsync ────────────────────────────

    [Fact]
    public async Task CreateCaseAsync_AddsAndReturnsCase()
    {
        var newCase = BuildCase(0, "CASE-NEW");
        newCase.Id = 0; // let EF assign

        var result = await Sut.CreateCaseAsync(newCase);

        Assert.True(result.Id > 0);
        Assert.Equal("CASE-NEW", result.CaseId);
    }

    [Fact]
    public async Task CreateCaseAsync_PersiststsToDatabase()
    {
        var newCase = BuildCase(0, "CASE-PERSIST");
        newCase.Id = 0;

        await Sut.CreateCaseAsync(newCase);

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Cases.FirstOrDefaultAsync(c => c.CaseId == "CASE-PERSIST");
        Assert.NotNull(saved);
    }

    // ──────────────────────────── UpdateCaseAsync ────────────────────────────

    [Fact]
    public async Task UpdateCaseAsync_WhenCaseExists_UpdatesScalarProperties()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var update = BuildCase(1);
        update.MemberName = "TSgt Jane Smith";
        update.Unit = "12 OG";

        var result = await Sut.UpdateCaseAsync(1, update);

        Assert.NotNull(result);
        Assert.Equal("TSgt Jane Smith", result.MemberName);
        Assert.Equal("12 OG", result.Unit);
    }

    [Fact]
    public async Task UpdateCaseAsync_WhenCaseNotFound_ReturnsNull()
    {
        var update = BuildCase(999);

        var result = await Sut.UpdateCaseAsync(999, update);

        Assert.Null(result);
    }

    [Theory]
    [ClassData(typeof(AuthorityCountTestData))]
    public async Task UpdateCaseAsync_SyncsAuthoritiesToMatchIncoming(int incomingCount)
    {
        await using var seedCtx = CreateSeedContext();
        var lodCase = BuildCase(1);
        // Start with 2 existing authorities
        lodCase.Authorities.Add(new LineOfDutyAuthority { Role = "SJA",       LineOfDutyCaseId = 1 });
        lodCase.Authorities.Add(new LineOfDutyAuthority { Role = "WingCC",    LineOfDutyCaseId = 1 });
        seedCtx.Cases.Add(lodCase);
        await seedCtx.SaveChangesAsync();

        var update = BuildCase(1);
        for (int i = 0; i < incomingCount; i++)
        {
            update.Authorities.Add(new LineOfDutyAuthority { Role = $"Role-{i}", LineOfDutyCaseId = 1 });
        }

        await Sut.UpdateCaseAsync(1, update);

        await using var verifyCtx = CreateSeedContext();
        var finalCount = await verifyCtx.Authorities.CountAsync(a => a.LineOfDutyCaseId == 1);
        Assert.Equal(incomingCount, finalCount);
    }

    [Fact]
    public async Task UpdateCaseAsync_PreservesMemberIdForeignKey()
    {
        await using var seedCtx = CreateSeedContext();
        var member = new Member { FirstName = "John", LastName = "Doe" };
        seedCtx.Members.Add(member);
        await seedCtx.SaveChangesAsync();

        var lodCase = BuildCase(1);
        lodCase.MemberId = member.Id;
        seedCtx.Cases.Add(lodCase);
        await seedCtx.SaveChangesAsync();

        var update = BuildCase(1);
        update.MemberId = 0; // attempt to clear MemberId via update

        var result = await Sut.UpdateCaseAsync(1, update);

        Assert.Equal(member.Id, result.MemberId); // MemberId is preserved
    }

    // ──────────────────────────── DeleteCaseAsync ────────────────────────────

    [Theory]
    [ClassData(typeof(CaseKeyExistsTestData))]
    public async Task DeleteCaseAsync_ReturnsExpectedResult(int key, bool seedCase)
    {
        if (seedCase)
        {
            await using var ctx = CreateSeedContext();
            ctx.Cases.Add(BuildCase(key));
            await ctx.SaveChangesAsync();
        }

        var result = await Sut.DeleteCaseAsync(key);

        Assert.Equal(seedCase, result);
    }

    [Fact]
    public async Task DeleteCaseAsync_WhenCaseExists_RemovesCaseFromDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        await Sut.DeleteCaseAsync(1);

        await using var verifyCtx = CreateSeedContext();
        Assert.False(await verifyCtx.Cases.AnyAsync(c => c.Id == 1));
    }

    [Fact]
    public async Task DeleteCaseAsync_WhenCaseHasRelatedData_RemovesAllRelatedEntities()
    {
        await using var seedCtx = CreateSeedContext();
        var lodCase = BuildCase(1);
        lodCase.Documents.Add(new LineOfDutyDocument   { FileName = "doc.pdf"   });
        lodCase.Authorities.Add(new LineOfDutyAuthority { Role = "Commander"    });
        lodCase.Notifications.Add(new Notification      { Title = "Notif #1"    });
        seedCtx.Cases.Add(lodCase);
        await seedCtx.SaveChangesAsync();

        await Sut.DeleteCaseAsync(1);

        await using var verifyCtx = CreateSeedContext();
        Assert.False(await verifyCtx.Documents.AnyAsync(d => d.LineOfDutyCaseId == 1));
        Assert.False(await verifyCtx.Authorities.AnyAsync(a => a.LineOfDutyCaseId == 1));
        Assert.False(await verifyCtx.Notifications.AnyAsync(n => n.LineOfDutyCaseId == 1));
    }

    // ──────────────────────────── PatchCaseAsync ────────────────────────────

    [Fact]
    public async Task PatchCaseAsync_WhenCaseExists_AppliesDeltaChanges()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var delta = new Delta<LineOfDutyCase>();
        delta.TrySetPropertyValue(nameof(LineOfDutyCase.MemberName), "Patched Name");
        delta.TrySetPropertyValue(nameof(LineOfDutyCase.Unit),       "Patched Unit");

        var result = await Sut.PatchCaseAsync(1, delta);

        Assert.NotNull(result);
        Assert.Equal("Patched Name", result.MemberName);
        Assert.Equal("Patched Unit", result.Unit);
    }

    [Fact]
    public async Task PatchCaseAsync_WhenCaseNotFound_ReturnsNull()
    {
        var delta = new Delta<LineOfDutyCase>();
        delta.TrySetPropertyValue(nameof(LineOfDutyCase.MemberName), "Ghost");

        var result = await Sut.PatchCaseAsync(999, delta);

        Assert.Null(result);
    }

    [Fact]
    public async Task PatchCaseAsync_WhenCaseExists_OnlyChangesSpecifiedProperties()
    {
        await using var seedCtx = CreateSeedContext();
        var original = BuildCase(1);
        original.Unit = "Original Unit";
        original.MemberName = "Original Name";
        seedCtx.Cases.Add(original);
        await seedCtx.SaveChangesAsync();

        // Only patch MemberName; Unit should not change
        var delta = new Delta<LineOfDutyCase>();
        delta.TrySetPropertyValue(nameof(LineOfDutyCase.MemberName), "New Name");

        var result = await Sut.PatchCaseAsync(1, delta);

        Assert.Equal("New Name",     result.MemberName);
        Assert.Equal("Original Unit", result.Unit);
    }
}
