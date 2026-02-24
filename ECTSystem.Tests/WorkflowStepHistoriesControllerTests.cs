using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class WorkflowStepHistoriesControllerTests : ControllerTestBase
{
    private readonly Mock<IWorkflowStepHistoryService>  _mockService;
    private readonly WorkflowStepHistoriesController    _sut;

    public WorkflowStepHistoriesControllerTests()
    {
        _mockService = new Mock<IWorkflowStepHistoryService>();
        _sut         = new WorkflowStepHistoriesController(_mockService.Object);
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

        var r = Assert.IsType<CreatedODataResult<WorkflowStepHistory>>(result);
        Assert.Equal(entry, r.Value);
    }

    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("WorkflowState", "Required");

        var result = await _sut.Post(new WorkflowStepHistory(), CancellationToken.None);

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

        await _sut.Post(new WorkflowStepHistory(), CancellationToken.None);

        _mockService.Verify(s => s.AddHistoryEntryAsync(It.IsAny<WorkflowStepHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─────────────────────────────── Helpers ─────────────────────────────────

    private static WorkflowStepHistory BuildEntry() => new WorkflowStepHistory
    {
        LineOfDutyCaseId = 1,
        WorkflowState    = LineOfDutyWorkflowState.MemberInformationEntry,
        Action           = TransitionAction.Entered,
        Status           = WorkflowStepStatus.InProgress,
        OccurredAt       = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
