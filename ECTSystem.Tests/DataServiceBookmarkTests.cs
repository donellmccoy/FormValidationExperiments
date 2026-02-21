using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;
using ECTSystem.Tests.TestData;
using Xunit;

namespace ECTSystem.Tests;

public class DataServiceBookmarkTests : DataServiceTestBase
{
    // ─────────────────────────── GetBookmarksQueryable ───────────────────────────

    [Fact]
    public async Task GetBookmarksQueryable_ReturnsOnlyBookmarksForSpecifiedUser()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        ctx.Cases.Add(BuildCase(2, "CASE-0002"));
        await ctx.SaveChangesAsync();

        ctx.CaseBookmarks.AddRange(
            new CaseBookmark { UserId = "alice", LineOfDutyCaseId = 1, BookmarkedDate = DateTime.UtcNow },
            new CaseBookmark { UserId = "alice", LineOfDutyCaseId = 2, BookmarkedDate = DateTime.UtcNow },
            new CaseBookmark { UserId = "bob",   LineOfDutyCaseId = 1, BookmarkedDate = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var queryable = Sut.GetBookmarksQueryable("alice");
        var result = await queryable.ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.Equal("alice", b.UserId));
    }

    [Fact]
    public async Task GetBookmarksQueryable_WhenNoBookmarksForUser_ReturnsEmptyQueryable()
    {
        var queryable = Sut.GetBookmarksQueryable("unknown-user");
        var result = await queryable.ToListAsync();

        Assert.Empty(result);
    }

    // ─────────────────────── GetBookmarkedCasesQueryable ─────────────────────────

    [Fact]
    public async Task GetBookmarkedCasesQueryable_ReturnsOnlyCasesBookmarkedByUser()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.AddRange(BuildCase(1, "CASE-0001"), BuildCase(2, "CASE-0002"), BuildCase(3, "CASE-0003"));
        await ctx.SaveChangesAsync();

        ctx.CaseBookmarks.AddRange(
            new CaseBookmark { UserId = "alice", LineOfDutyCaseId = 1, BookmarkedDate = DateTime.UtcNow },
            new CaseBookmark { UserId = "alice", LineOfDutyCaseId = 3, BookmarkedDate = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var queryable = Sut.GetBookmarkedCasesQueryable("alice");
        var result = await queryable.ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Id == 1);
        Assert.Contains(result, c => c.Id == 3);
        Assert.DoesNotContain(result, c => c.Id == 2); // not bookmarked
    }

    // ──────────────────────────── AddBookmarkAsync ───────────────────────────────

    [Theory]
    [ClassData(typeof(BookmarkUserCaseTestData))]
    public async Task AddBookmarkAsync_WhenNotAlreadyBookmarked_CreatesBookmarkForUser(string userId, int caseId)
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(caseId, $"CASE-{caseId:D4}"));
        await ctx.SaveChangesAsync();

        var result = await Sut.AddBookmarkAsync(userId, caseId);

        Assert.True(result.Id > 0);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(caseId, result.LineOfDutyCaseId);
    }

    [Fact]
    public async Task AddBookmarkAsync_WhenAlreadyBookmarked_ReturnsExistingBookmarkWithoutDuplicate()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        // Add bookmark twice for the same user/case
        var first  = await Sut.AddBookmarkAsync("alice", 1);
        var second = await Sut.AddBookmarkAsync("alice", 1);

        Assert.Equal(first.Id, second.Id);

        await using var verifyCtx = CreateSeedContext();
        var count = await verifyCtx.CaseBookmarks
            .CountAsync(b => b.UserId == "alice" && b.LineOfDutyCaseId == 1);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddBookmarkAsync_SetsBookmarkedDateToUtcNow()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var before = DateTime.UtcNow;
        var result = await Sut.AddBookmarkAsync("alice", 1);

        Assert.True(result.BookmarkedDate >= before.AddSeconds(-1));
        Assert.True(result.BookmarkedDate <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task AddBookmarkAsync_PersistsToDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        await Sut.AddBookmarkAsync("persist-user", 1);

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.CaseBookmarks
            .AnyAsync(b => b.UserId == "persist-user" && b.LineOfDutyCaseId == 1));
    }

    // ──────────────────────────── RemoveBookmarkAsync ────────────────────────────

    [Fact]
    public async Task RemoveBookmarkAsync_WhenBookmarkExists_ReturnsTrueAndRemovesEntry()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        seedCtx.CaseBookmarks.Add(new CaseBookmark
        {
            UserId          = "alice",
            LineOfDutyCaseId = 1,
            BookmarkedDate  = DateTime.UtcNow
        });
        await seedCtx.SaveChangesAsync();

        var result = await Sut.RemoveBookmarkAsync("alice", 1);

        Assert.True(result);
        await using var verifyCtx = CreateSeedContext();
        Assert.False(await verifyCtx.CaseBookmarks.AnyAsync(b => b.UserId == "alice" && b.LineOfDutyCaseId == 1));
    }

    [Fact]
    public async Task RemoveBookmarkAsync_WhenBookmarkNotFound_ReturnsFalse()
    {
        var result = await Sut.RemoveBookmarkAsync("ghost-user", 999);

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveBookmarkAsync_OnlyRemovesTargetUserBookmark_NotOtherUsers()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        seedCtx.CaseBookmarks.AddRange(
            new CaseBookmark { UserId = "alice", LineOfDutyCaseId = 1, BookmarkedDate = DateTime.UtcNow },
            new CaseBookmark { UserId = "bob",   LineOfDutyCaseId = 1, BookmarkedDate = DateTime.UtcNow }
        );
        await seedCtx.SaveChangesAsync();

        await Sut.RemoveBookmarkAsync("alice", 1);

        await using var verifyCtx = CreateSeedContext();
        Assert.False(await verifyCtx.CaseBookmarks.AnyAsync(b => b.UserId == "alice" && b.LineOfDutyCaseId == 1));
        Assert.True(await verifyCtx.CaseBookmarks.AnyAsync(b => b.UserId == "bob"   && b.LineOfDutyCaseId == 1));
    }

    // ──────────────────────────── IsBookmarkedAsync ──────────────────────────────

    [Theory]
    [ClassData(typeof(BookmarkExistsTestData))]
    public async Task IsBookmarkedAsync_ReturnsExpectedResult(bool seedBookmark, bool expectedResult)
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        if (seedBookmark)
        {
            ctx.CaseBookmarks.Add(new CaseBookmark
            {
                UserId           = "test-user",
                LineOfDutyCaseId = 1,
                BookmarkedDate   = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var result = await Sut.IsBookmarkedAsync("test-user", 1);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task IsBookmarkedAsync_DifferentUserForSameCase_ReturnsFalse()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        // Bookmark for alice only
        ctx.CaseBookmarks.Add(new CaseBookmark
        {
            UserId           = "alice",
            LineOfDutyCaseId = 1,
            BookmarkedDate   = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await Sut.IsBookmarkedAsync("bob", 1);

        Assert.False(result);
    }

    [Fact]
    public async Task IsBookmarkedAsync_SameUserForDifferentCase_ReturnsFalse()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        ctx.Cases.Add(BuildCase(2, "CASE-0002"));
        await ctx.SaveChangesAsync();

        // Bookmark case 1 for alice, then query case 2
        ctx.CaseBookmarks.Add(new CaseBookmark
        {
            UserId           = "alice",
            LineOfDutyCaseId = 1,
            BookmarkedDate   = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await Sut.IsBookmarkedAsync("alice", 2);

        Assert.False(result);
    }
}
