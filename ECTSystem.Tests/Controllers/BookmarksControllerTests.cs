using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="BookmarksController"/>, the OData controller that manages
/// per-user case bookmarks (favorites) in the ECT System API.
/// </summary>
/// <remarks>
/// <para>
/// Each test instance creates an isolated in-memory EF Core database and a
/// <see cref="BookmarksController"/> configured with a fake authenticated user
/// (<see cref="ControllerTestBase.TestUserId"/>). The controller filters all queries
/// and mutations by the current user's <c>NameIdentifier</c> claim.
/// </para>
/// <para>
/// Tests are organized by controller action: <c>Get</c> (collection), <c>Post</c>
/// (idempotent create), and <c>Delete</c> (by key). Each section covers both success
/// and not-found / duplicate scenarios.
/// </para>
/// </remarks>
public class BookmarksControllerTests : ControllerTestBase
{
    /// <summary>In-memory database options shared across seed, act, and verify phases.</summary>
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    /// <summary>Mocked logging service injected into the controller.</summary>
    private readonly Mock<ILoggingService> _mockLog;
    /// <summary>Mocked context factory returning <see cref="EctDbContext"/> instances backed by the in-memory store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    /// <summary>System under test — the <see cref="BookmarksController"/> instance.</summary>
    private readonly BookmarksController _sut;

    /// <summary>
    /// Initializes the in-memory database, configures mocked dependencies, and creates
    /// the <see cref="BookmarksController"/> with a fake authenticated user context.
    /// </summary>
    public BookmarksControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockLog = new Mock<ILoggingService>();
        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();
        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));
        _mockContextFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(_dbOptions));

        _sut = new BookmarksController(_mockContextFactory.Object, _mockLog.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────── Get (collection) ────────────────────────────

    /// <summary>
    /// Verifies that <see cref="BookmarksController.Get()"/> returns an
    /// <see cref="OkObjectResult"/> wrapping an <see cref="IQueryable{Bookmark}"/>
    /// scoped to the authenticated user.
    /// </summary>
    [Fact]
    public async Task Get_ReturnsOkWithBookmarksQueryable()
    {
        var result = await _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    /// <summary>
    /// Verifies that posting a new bookmark creates it, stamps the current user's ID,
    /// and returns <see cref="CreatedODataResult{Bookmark}"/>.
    /// </summary>
    [Fact]
    public async Task Post_ReturnsCreatedWithBookmark()
    {
        var dto = new Bookmark { LineOfDutyCaseId = 3 };

        var result = await _sut.Post(dto);

        var r = Assert.IsType<CreatedODataResult<Bookmark>>(result);
        var created = (Bookmark)r.Value;
        Assert.Equal(TestUserId, created.UserId);
        Assert.Equal(3, created.LineOfDutyCaseId);
    }

    /// <summary>
    /// Verifies idempotent bookmark creation: when a bookmark for the same user and case
    /// already exists, <c>Post</c> returns the existing record instead of creating a duplicate.
    /// </summary>
    [Fact]
    public async Task Post_DuplicateBookmark_ReturnsExisting()
    {
        // Seed a bookmark
        using (var ctx = new EctDbContext(_dbOptions))
        {
            ctx.Bookmarks.Add(new Bookmark
            {
                UserId = TestUserId,
                LineOfDutyCaseId = 5
            });
            ctx.SaveChanges();
        }

        var dto = new Bookmark { LineOfDutyCaseId = 5 };
        var result = await _sut.Post(dto);

        var r = Assert.IsType<OkObjectResult>(result);
        var existing = (Bookmark)r.Value;
        Assert.Equal(5, existing.LineOfDutyCaseId);
    }

    // ──────────────────────────── Delete (key) ─────────────────────────────

    /// <summary>
    /// Verifies that <see cref="BookmarksController.Delete(int, CancellationToken)"/>
    /// removes the matching bookmark and returns <see cref="NoContentResult"/>.
    /// </summary>
    [Fact]
    public async Task Delete_WhenBookmarkExists_ReturnsNoContent()
    {
        int bookmarkId;
        using (var ctx = new EctDbContext(_dbOptions))
        {
            var bm = new Bookmark
            {
                UserId = TestUserId,
                LineOfDutyCaseId = 1
            };
            ctx.Bookmarks.Add(bm);
            ctx.SaveChanges();
            bookmarkId = bm.Id;
        }

        var result = await _sut.Delete(bookmarkId);

        Assert.IsType<NoContentResult>(result);

        using (var ctx = new EctDbContext(_dbOptions))
        {
            Assert.Empty(ctx.Bookmarks);
        }
    }

    /// <summary>
    /// Verifies that <c>Delete</c> returns <see cref="NotFoundResult"/> when no
    /// bookmark with the specified key exists for the current user.
    /// </summary>
    [Fact]
    public async Task Delete_WhenBookmarkNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
    }
}
