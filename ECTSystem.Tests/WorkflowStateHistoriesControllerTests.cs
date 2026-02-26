using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class WorkflowStateHistoriesControllerTests : ControllerTestBase
{
    private readonly Mock<IWorkflowStateHistoryService>  _mockService;
    private readonly WorkflowStateHistoriesController    _sut;

    public WorkflowStateHistoriesControllerTests()
    {
        _mockService = new Mock<IWorkflowStateHistoryService>();
        _sut         = new WorkflowStateHistoriesController(_mockService.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    [Fact]
    public async Task Post_WhenModelValid_ReturnsCreatedWithEntry()
    {
        var entry = BuildEntry();
        _mockService.Setup(s => s.AddHistoryEntryAsync(entry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var result = await _sut.Post(entry, CancellationToken.None);

        var r = Assert.IsType<CreatedODataResult<WorkflowStateHistory>>(result);
        Assert.Equal(entry, r.Value);
    }

    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("WorkflowState", "Required");

        var result = await _sut.Post(new WorkflowStateHistory(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Post_WhenModelValid_InvokesServiceWithEntry()
    {
        var entry = BuildEntry();
        _mockService.Setup(s => s.AddHistoryEntryAsync(entry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        await _sut.Post(entry, CancellationToken.None);

        _mockService.Verify(s => s.AddHistoryEntryAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_WhenModelInvalid_DoesNotInvokeService()
    {
        _sut.ModelState.AddModelError("key", "error");

        await _sut.Post(new WorkflowStateHistory(), CancellationToken.None);

        _mockService.Verify(s => s.AddHistoryEntryAsync(It.IsAny<WorkflowStateHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─────────────────────────────── Helpers ─────────────────────────────────

    private static WorkflowStateHistory BuildEntry() => new WorkflowStateHistory
    {
        LineOfDutyCaseId = 1,
        WorkflowState    = WorkflowState.MemberInformationEntry,
        Action           = TransitionAction.Entered,
        Status           = WorkflowStepStatus.InProgress,
        OccurredAt       = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
