using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.OData.Edm;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class CasesControllerTests : ControllerTestBase
{
    private readonly Mock<IDataService>         _mockDataService;
    private readonly Mock<ICaseBookmarkService> _mockBookmarkService;
    private readonly Mock<IApiLogService>       _mockLog;
    private readonly Mock<IEdmModel>            _mockEdmModel;
    private readonly CasesController            _sut;

    public CasesControllerTests()
    {
        _mockDataService     = new Mock<IDataService>();
        _mockBookmarkService = new Mock<ICaseBookmarkService>();
        _mockLog             = new Mock<IApiLogService>();
        _mockEdmModel        = new Mock<IEdmModel>();

        _sut = new CasesController(
            _mockDataService.Object,
            _mockLog.Object,
            _mockBookmarkService.Object,
            _mockEdmModel.Object);

        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────── Get (collection) ────────────────────────────

    [Fact]
    public void Get_ReturnsOkContainingCasesQueryable()
    {
        _mockDataService.Setup(s => s.GetCasesQueryable())
            .Returns(new List<LineOfDutyCase> { BuildCase(1) }.AsQueryable());

        var result = _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────── Get (by key) ────────────────────────────────

    [Fact]
    public async Task GetByKey_WhenCaseExists_ReturnsOkWithCase()
    {
        var lodCase = BuildCase(1);
        _mockDataService.Setup(s => s.GetCaseByKeyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lodCase);

        var result = await _sut.Get(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(lodCase, ok.Value);
    }

    [Fact]
    public async Task GetByKey_WhenCaseNotFound_ReturnsNotFound()
    {
        _mockDataService.Setup(s => s.GetCaseByKeyAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LineOfDutyCase)null);

        var result = await _sut.Get(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    [Fact]
    public async Task Post_WhenModelValid_ReturnsCreatedWithCase()
    {
        var lodCase = BuildCase(0, "CASE-NEW");
        var created = BuildCase(1, "CASE-NEW");
        _mockDataService.Setup(s => s.CreateCaseAsync(lodCase, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Post(lodCase);

        Assert.IsType<CreatedODataResult<LineOfDutyCase>>(result);
    }

    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("MemberName", "Required");

        var result = await _sut.Post(new LineOfDutyCase());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ─────────────────────────────── Patch ───────────────────────────────────

    [Fact]
    public async Task Patch_WhenCaseExists_ReturnsUpdated()
    {
        var lodCase = BuildCase(1);
        var delta   = new Delta<LineOfDutyCase>();
        delta.TrySetPropertyValue(nameof(LineOfDutyCase.MemberName), "Updated Name");

        _mockDataService
            .Setup(s => s.PatchCaseAsync(1, It.IsAny<Delta<LineOfDutyCase>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lodCase);

        var result = await _sut.Patch(1, delta);

        Assert.IsType<UpdatedODataResult<LineOfDutyCase>>(result);
    }

    [Fact]
    public async Task Patch_WhenCaseNotFound_ReturnsNotFound()
    {
        var delta = new Delta<LineOfDutyCase>();
        _mockDataService
            .Setup(s => s.PatchCaseAsync(999, It.IsAny<Delta<LineOfDutyCase>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LineOfDutyCase)null);

        var result = await _sut.Patch(999, delta);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Patch_WhenDeltaIsNull_ReturnsBadRequest()
    {
        var result = await _sut.Patch(1, null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Patch_WhenModelStateInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("key", "error");
        var delta = new Delta<LineOfDutyCase>();

        var result = await _sut.Patch(1, delta);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ─────────────────────────────── Delete ──────────────────────────────────

    [Fact]
    public async Task Delete_WhenCaseExists_ReturnsNoContent()
    {
        _mockDataService.Setup(s => s.DeleteCaseAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.Delete(1);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenCaseNotFound_ReturnsNotFound()
    {
        _mockDataService.Setup(s => s.DeleteCaseAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Delete(999);

        Assert.IsType<NotFoundResult>(result);
    }
}
