using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class DataServiceAppealTests : DataServiceTestBase
{
    // ────────────────────────── GetAppealsByCaseIdAsync ─────────────────────────

    [Fact]
    public async Task GetAppealsByCaseIdAsync_WhenAppealsExist_ReturnsAppealsForCase()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        ctx.Cases.Add(BuildCase(2, "CASE-0002"));
        await ctx.SaveChangesAsync();

        ctx.Appeals.AddRange(
            new LineOfDutyAppeal { LineOfDutyCaseId = 1, Appellant = "John Doe",  AppealDate = DateTime.UtcNow },
            new LineOfDutyAppeal { LineOfDutyCaseId = 1, Appellant = "Jane Doe",  AppealDate = DateTime.UtcNow },
            new LineOfDutyAppeal { LineOfDutyCaseId = 2, Appellant = "Other Guy", AppealDate = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var result = await Sut.GetAppealsByCaseIdAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(1, a.LineOfDutyCaseId));
    }

    [Fact]
    public async Task GetAppealsByCaseIdAsync_WhenNoAppealsForCase_ReturnsEmptyList()
    {
        var result = await Sut.GetAppealsByCaseIdAsync(99);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAppealsByCaseIdAsync_IncludesAppellateAuthority()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var authority = new LineOfDutyAuthority { Role = "AppellateAuthority", Name = "Brig Gen Smith" };
        ctx.Authorities.Add(authority);
        await ctx.SaveChangesAsync();

        ctx.Appeals.Add(new LineOfDutyAppeal
        {
            LineOfDutyCaseId      = 1,
            Appellant             = "A1C Test",
            AppealDate            = DateTime.UtcNow,
            AppellateAuthorityId  = authority.Id
        });
        await ctx.SaveChangesAsync();

        var result = await Sut.GetAppealsByCaseIdAsync(1);

        Assert.Single(result);
        Assert.NotNull(result[0].AppellateAuthority);
        Assert.Equal("Brig Gen Smith", result[0].AppellateAuthority.Name);
    }

    // ────────────────────────────── AddAppealAsync ───────────────────────────────

    [Fact]
    public async Task AddAppealAsync_AddsAndReturnsAppeal()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var appeal = new LineOfDutyAppeal
        {
            LineOfDutyCaseId = 1,
            Appellant        = "SSgt Green",
            AppealDate       = new DateTime(2025, 3, 10),
            OriginalFinding  = LineOfDutyFinding.NotInLineOfDutyDueToMisconduct,
            AppealOutcome    = LineOfDutyFinding.InLineOfDuty
        };

        var result = await Sut.AddAppealAsync(appeal);

        Assert.True(result.Id > 0);
        Assert.Equal("SSgt Green", result.Appellant);
    }

    [Fact]
    public async Task AddAppealAsync_PersistsToDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        await Sut.AddAppealAsync(new LineOfDutyAppeal
        {
            LineOfDutyCaseId = 1,
            Appellant        = "Persist Test",
            AppealDate       = DateTime.UtcNow
        });

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.Appeals.AnyAsync(a => a.Appellant == "Persist Test"));
    }
}

public class DataServiceAuthorityTests : DataServiceTestBase
{
    // ────────────────────── GetAuthoritiesByCaseIdAsync ─────────────────────────

    [Fact]
    public async Task GetAuthoritiesByCaseIdAsync_ReturnsAuthoritiesForCase()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        ctx.Cases.Add(BuildCase(2, "CASE-0002"));
        await ctx.SaveChangesAsync();

        ctx.Authorities.AddRange(
            new LineOfDutyAuthority { LineOfDutyCaseId = 1, Role = "Commander"   },
            new LineOfDutyAuthority { LineOfDutyCaseId = 1, Role = "SJA"         },
            new LineOfDutyAuthority { LineOfDutyCaseId = 2, Role = "WingCC"      }
        );
        await ctx.SaveChangesAsync();

        var result = await Sut.GetAuthoritiesByCaseIdAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(1, a.LineOfDutyCaseId));
    }

    [Fact]
    public async Task GetAuthoritiesByCaseIdAsync_WhenNoAuthoritiesForCase_ReturnsEmptyList()
    {
        var result = await Sut.GetAuthoritiesByCaseIdAsync(99);

        Assert.Empty(result);
    }

    // ────────────────────────── AddAuthorityAsync ────────────────────────────────

    [Fact]
    public async Task AddAuthorityAsync_AddsAndReturnsAuthority()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var authority = new LineOfDutyAuthority
        {
            LineOfDutyCaseId = 1,
            Role             = "Wing Commander",
            Name             = "Col Anderson",
            Rank             = "Col",
            Title            = "Wing CC"
        };

        var result = await Sut.AddAuthorityAsync(authority);

        Assert.True(result.Id > 0);
        Assert.Equal("Col Anderson", result.Name);
        Assert.Equal("Wing Commander", result.Role);
    }

    [Fact]
    public async Task AddAuthorityAsync_PersistsToDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        await Sut.AddAuthorityAsync(new LineOfDutyAuthority
        {
            LineOfDutyCaseId = 1,
            Role             = "SJA",
            Name             = "Maj Legal"
        });

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.Authorities.AnyAsync(a => a.Name == "Maj Legal"));
    }

    [Theory]
    [InlineData("Commander")]
    [InlineData("SJA")]
    [InlineData("WingCC")]
    [InlineData("AppointingAuthority")]
    public async Task AddAuthorityAsync_StoringDifferentRoles_AllPersist(string role)
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var result = await Sut.AddAuthorityAsync(new LineOfDutyAuthority
        {
            LineOfDutyCaseId = 1,
            Role             = role,
            Name             = $"Test {role}"
        });

        Assert.Equal(role, result.Role);
        Assert.True(result.Id > 0);
    }
}
