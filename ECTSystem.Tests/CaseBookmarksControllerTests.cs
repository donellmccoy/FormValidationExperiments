using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class CaseBookmarksControllerTests : ControllerTestBase
{
    private readonly Mock<ICaseBookmarkService> _mockService;
    private readonly CaseBookmarksController   _sut;

    public CaseBookmarksControllerTests()
    {
        _mockService = new Mock<ICaseBookmarkService>();
        _sut         = new CaseBookmarksController(_mockService.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────── Get (collection) ────────────────────────────

    [Fact]
    public void Get_ReturnsOkWithBookmarksQueryable()
    {
        _mockService.Setup(s => s.GetBookmarksQueryable(TestUserId))
            .Returns(new List<CaseBookmark>().AsQueryable());

        var result = _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    [Fact]
    public async Task Post_ReturnsCreatedWithBookmark()
    {
        var bookmark = new CaseBookmark { LineOfDutyCaseId = 3 };
        var created  = new CaseBookmark { Id = 10, UserId = TestUserId, LineOfDutyCaseId = 3, BookmarkedDate = DateTime.UtcNow };

        _mockService.Setup(s => s.AddBookmarkAsync(TestUserId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Post(bookmark);

        var r = Assert.IsType<CreatedODataResult<CaseBookmark>>(result);
        Assert.Equal(created, r.Value);
    }

    [Fact]
    public async Task Post_PassesCurrentUserIdToService()
    {
        var bookmark = new CaseBookmark { LineOfDutyCaseId = 5 };
        _mockService.Setup(s => s.AddBookmarkAsync(TestUserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaseBookmark { Id = 1, UserId = TestUserId, LineOfDutyCaseId = 5 });

        await _sut.Post(bookmark);

        _mockService.Verify(s => s.AddBookmarkAsync(TestUserId, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────── DeleteByCaseId ──────────────────────────────

    [Fact]
    public async Task DeleteByCaseId_WhenBookmarkExists_ReturnsNoContent()
    {
        _mockService.Setup(s => s.RemoveBookmarkAsync(TestUserId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeleteByCaseId(1);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteByCaseId_WhenBookmarkNotFound_ReturnsNotFound()
    {
        _mockService.Setup(s => s.RemoveBookmarkAsync(TestUserId, 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.DeleteByCaseId(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────── IsBookmarked ────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsBookmarked_ReturnsOkWithExpectedValue(bool expected)
    {
        _mockService.Setup(s => s.IsBookmarkedAsync(TestUserId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.IsBookmarked(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        // The anonymous type { Value = bool } — reflect the property to avoid casting issues
        var value = ok.Value.GetType().GetProperty("Value")!.GetValue(ok.Value);
        Assert.Equal(expected, value);
    }
}
