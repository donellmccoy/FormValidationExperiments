using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class DataServiceTimelineTests : DataServiceTestBase
{
    // ────────────────────── GetTimelineStepsByCaseIdAsync ────────────────────────

    [Fact]
    public async Task GetTimelineStepsByCaseIdAsync_ReturnsStepsForCase()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        ctx.Cases.Add(BuildCase(2, "CASE-0002"));
        await ctx.SaveChangesAsync();

        ctx.TimelineSteps.AddRange(
            new TimelineStep { LineOfDutyCaseId = 1, StepDescription = "Member Reports",        TimelineDays = 5  },
            new TimelineStep { LineOfDutyCaseId = 1, StepDescription = "Medical Review",         TimelineDays = 10 },
            new TimelineStep { LineOfDutyCaseId = 2, StepDescription = "Unrelated Case Step",    TimelineDays = 3  }
        );
        await ctx.SaveChangesAsync();

        var result = await Sut.GetTimelineStepsByCaseIdAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(1, s.LineOfDutyCaseId));
    }

    [Fact]
    public async Task GetTimelineStepsByCaseIdAsync_WhenNoSteps_ReturnsEmptyList()
    {
        var result = await Sut.GetTimelineStepsByCaseIdAsync(99);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTimelineStepsByCaseIdAsync_IncludesResponsibleAuthority()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var authority = new LineOfDutyAuthority { Role = "MedicalProvider", Name = "Capt Medic" };
        ctx.Authorities.Add(authority);
        await ctx.SaveChangesAsync();

        ctx.TimelineSteps.Add(new TimelineStep
        {
            LineOfDutyCaseId     = 1,
            StepDescription      = "Medical Assessment",
            TimelineDays         = 10,
            ResponsibleAuthorityId = authority.Id
        });
        await ctx.SaveChangesAsync();

        var result = await Sut.GetTimelineStepsByCaseIdAsync(1);

        Assert.Single(result);
        Assert.NotNull(result[0].ResponsibleAuthority);
        Assert.Equal("Capt Medic", result[0].ResponsibleAuthority.Name);
    }

    // ──────────────────────────── AddTimelineStepAsync ───────────────────────────

    [Fact]
    public async Task AddTimelineStepAsync_AddsAndReturnsStep()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var step = new TimelineStep
        {
            LineOfDutyCaseId = 1,
            StepDescription  = "LOD Initiation",
            TimelineDays     = 5,
            StartDate        = new DateTime(2025, 1, 15),
            IsOptional       = false
        };

        var result = await Sut.AddTimelineStepAsync(step);

        Assert.True(result.Id > 0);
        Assert.Equal("LOD Initiation", result.StepDescription);
        Assert.Equal(5, result.TimelineDays);
    }

    [Fact]
    public async Task AddTimelineStepAsync_PersistsToDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        await Sut.AddTimelineStepAsync(new TimelineStep
        {
            LineOfDutyCaseId = 1,
            StepDescription  = "Commander Review",
            TimelineDays     = 15
        });

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.TimelineSteps.AnyAsync(s => s.StepDescription == "Commander Review"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(90)]
    public async Task AddTimelineStepAsync_StoresCorrectTimelineDays(int days)
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var result = await Sut.AddTimelineStepAsync(new TimelineStep
        {
            LineOfDutyCaseId = 1,
            StepDescription  = $"Step-{days}d",
            TimelineDays     = days
        });

        Assert.Equal(days, result.TimelineDays);
    }

    // ─────────────────────────── UpdateTimelineStepAsync ─────────────────────────

    [Fact]
    public async Task UpdateTimelineStepAsync_WhenStepExists_UpdatesAndReturnsStep()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var step = new TimelineStep
        {
            LineOfDutyCaseId = 1,
            StepDescription  = "Original Step",
            TimelineDays     = 5
        };
        seedCtx.TimelineSteps.Add(step);
        await seedCtx.SaveChangesAsync();

        var update = new TimelineStep
        {
            Id               = step.Id,
            LineOfDutyCaseId = 1,
            StepDescription  = "Updated Step",
            TimelineDays     = 10,
            CompletionDate   = new DateTime(2025, 3, 1)
        };

        var result = await Sut.UpdateTimelineStepAsync(update);

        Assert.Equal("Updated Step", result.StepDescription);
        Assert.Equal(10, result.TimelineDays);
        Assert.Equal(new DateTime(2025, 3, 1), result.CompletionDate);
    }

    [Fact]
    public async Task UpdateTimelineStepAsync_WhenStepNotFound_ThrowsInvalidOperationException()
    {
        var nonExistentStep = new TimelineStep
        {
            Id               = 9999,
            LineOfDutyCaseId = 1,
            StepDescription  = "Ghost Step",
            TimelineDays     = 5
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sut.UpdateTimelineStepAsync(nonExistentStep));
    }

    [Fact]
    public async Task UpdateTimelineStepAsync_PersistsChangesToDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var step = new TimelineStep
        {
            LineOfDutyCaseId = 1,
            StepDescription  = "Before Update",
            TimelineDays     = 5
        };
        seedCtx.TimelineSteps.Add(step);
        await seedCtx.SaveChangesAsync();

        await Sut.UpdateTimelineStepAsync(new TimelineStep
        {
            Id               = step.Id,
            LineOfDutyCaseId = 1,
            StepDescription  = "After Update",
            TimelineDays     = 20
        });

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.TimelineSteps.FindAsync(step.Id);
        Assert.Equal("After Update", saved.StepDescription);
        Assert.Equal(20, saved.TimelineDays);
    }
}
