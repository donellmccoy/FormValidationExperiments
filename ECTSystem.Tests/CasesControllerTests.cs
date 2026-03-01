using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class CasesControllerTests : ControllerTestBase
{
    private readonly Mock<ILoggingService>       _mockLog;
    private readonly Mock<IEdmModel>            _mockEdmModel;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly DbContextOptions<EctDbContext>        _dbOptions;
    private readonly CasesController            _sut;

    public CasesControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        // Seed a default Member so that FK references are valid for Include queries
        using (var seedCtx = new EctDbContext(_dbOptions))
        {
            seedCtx.Members.Add(new Member
            {
                Id = 1, FirstName = "John", LastName = "Doe",
                Rank = "SSgt", Unit = "99 ABW"
            });
            seedCtx.SaveChanges();
        }

        _mockLog             = new Mock<ILoggingService>();
        _mockEdmModel        = new Mock<IEdmModel>();
        _mockContextFactory  = new Mock<IDbContextFactory<EctDbContext>>();

        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));

        _mockContextFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(_dbOptions));

        _sut = new CasesController(
            _mockLog.Object,
            _mockEdmModel.Object,
            _mockContextFactory.Object);

        _sut.ControllerContext = CreateControllerContext();
    }

    private EctDbContext CreateSeedContext() => new EctDbContext(_dbOptions);

    private void SeedCase(LineOfDutyCase lodCase)
    {
        using var ctx = CreateSeedContext();
        ctx.Cases.Add(lodCase);
        ctx.SaveChanges();
    }

    // ─────────────────────────── Get (collection) ────────────────────────────

    [Fact]
    public void Get_ReturnsOkContainingCasesQueryable()
    {
        SeedCase(BuildCase(1));

        var result = _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────── Get (by key) ────────────────────────────────

    [Fact]
    public async Task GetByKey_WhenCaseExists_ReturnsOkWithCase()
    {
        SeedCase(BuildCase(1));

        var result = await _sut.Get(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<LineOfDutyCase>(ok.Value);
        Assert.Equal(1, returned.Id);
    }

    [Fact]
    public async Task GetByKey_WhenCaseNotFound_ReturnsNotFound()
    {
        var result = await _sut.Get(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    [Fact]
    public async Task Post_WhenModelValid_ReturnsCreatedWithCase()
    {
        var lodCase = BuildCase(0, "CASE-NEW");

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
        SeedCase(BuildCase(1));
        var delta = new Delta<LineOfDutyCase>();
        delta.TrySetPropertyValue(nameof(LineOfDutyCase.MemberName), "Updated Name");

        var result = await _sut.Patch(1, delta);

        Assert.IsType<UpdatedODataResult<LineOfDutyCase>>(result);
    }

    [Fact]
    public async Task Patch_WhenCaseNotFound_ReturnsNotFound()
    {
        var delta = new Delta<LineOfDutyCase>();

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
        SeedCase(BuildCase(1));

        var result = await _sut.Delete(1);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenCaseNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999);

        Assert.IsType<NotFoundResult>(result);
    }
}
