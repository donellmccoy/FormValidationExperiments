using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class CaseBookmarksControllerTests : ControllerTestBase
{
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    private readonly Mock<IApiLogService> _mockLog;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly CaseBookmarksController _sut;

    public CaseBookmarksControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockLog = new Mock<IApiLogService>();
        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();
        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));
        _mockContextFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(_dbOptions));

        _sut = new CaseBookmarksController(_mockLog.Object, _mockContextFactory.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────── Get (collection) ────────────────────────────

    [Fact]
    public void Get_ReturnsOkWithBookmarksQueryable()
    {
        var result = _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    [Fact]
    public async Task Post_ReturnsCreatedWithBookmark()
    {
        var bookmark = new CaseBookmark { LineOfDutyCaseId = 3 };

        var result = await _sut.Post(bookmark);

        var r = Assert.IsType<CreatedODataResult<CaseBookmark>>(result);
        var created = (CaseBookmark)r.Value;
        Assert.Equal(TestUserId, created.UserId);
        Assert.Equal(3, created.LineOfDutyCaseId);
    }

    [Fact]
    public async Task Post_DuplicateBookmark_ReturnsExisting()
    {
        // Seed a bookmark
        using (var ctx = new EctDbContext(_dbOptions))
        {
            ctx.CaseBookmarks.Add(new CaseBookmark
            {
                UserId = TestUserId,
                LineOfDutyCaseId = 5,
                BookmarkedDate = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }

        var result = await _sut.Post(new CaseBookmark { LineOfDutyCaseId = 5 });

        var r = Assert.IsType<CreatedODataResult<CaseBookmark>>(result);
        var existing = (CaseBookmark)r.Value;
        Assert.Equal(5, existing.LineOfDutyCaseId);
    }

    // ─────────────────────────── DeleteByCaseId ──────────────────────────────

    [Fact]
    public async Task DeleteByCaseId_WhenBookmarkExists_ReturnsNoContent()
    {
        using (var ctx = new EctDbContext(_dbOptions))
        {
            ctx.CaseBookmarks.Add(new CaseBookmark
            {
                UserId = TestUserId,
                LineOfDutyCaseId = 1,
                BookmarkedDate = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }

        var result = await _sut.DeleteByCaseId(1);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteByCaseId_WhenBookmarkNotFound_ReturnsNotFound()
    {
        var result = await _sut.DeleteByCaseId(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────── IsBookmarked ────────────────────────────────

    [Fact]
    public async Task IsBookmarked_WhenBookmarkExists_ReturnsTrue()
    {
        using (var ctx = new EctDbContext(_dbOptions))
        {
            ctx.CaseBookmarks.Add(new CaseBookmark
            {
                UserId = TestUserId,
                LineOfDutyCaseId = 1,
                BookmarkedDate = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }

        var result = await _sut.IsBookmarked(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var value = ok.Value.GetType().GetProperty("Value")!.GetValue(ok.Value);
        Assert.Equal(true, value);
    }

    [Fact]
    public async Task IsBookmarked_WhenBookmarkNotFound_ReturnsFalse()
    {
        var result = await _sut.IsBookmarked(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var value = ok.Value.GetType().GetProperty("Value")!.GetValue(ok.Value);
        Assert.Equal(false, value);
    }
}
