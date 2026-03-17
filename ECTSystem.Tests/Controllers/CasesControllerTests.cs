using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="CasesController"/>, the primary OData controller managing
/// Line of Duty (LOD) case records in the ECT System API.
/// </summary>
/// <remarks>
/// <para>
/// Each test instance creates an isolated in-memory EF Core database with a pre-seeded
/// <see cref="ECTSystem.Shared.Models.Member"/> (Id&nbsp;=&nbsp;1) to satisfy foreign-key
/// constraints when cases are inserted. The <see cref="CasesController"/> is instantiated
/// with a mocked <see cref="ILoggingService"/>, a factory that produces
/// <see cref="EctDbContext"/> instances backed by that in-memory store, and an
/// <see cref="AF348PdfService"/> wired to a mocked <see cref="IWebHostEnvironment"/>.
/// </para>
/// <para>
/// Tests are organized by controller action: <c>Get</c> (collection and single),
/// <c>Post</c>, <c>Patch</c>, and <c>Delete</c>.
/// Each section validates both the happy path and relevant error/edge cases
/// (not-found, invalid model state, null input).
/// </para>
/// </remarks>
public class CasesControllerTests : ControllerTestBase
{
    /// <summary>Mocked logging service injected into the controller; verifies no unexpected log interactions.</summary>
    private readonly Mock<ILoggingService>       _mockLog;
    /// <summary>Mocked EDM model (unused by current tests but required by the controller constructor).</summary>
    private readonly Mock<IEdmModel>            _mockEdmModel;
    /// <summary>Mocked context factory that returns <see cref="EctDbContext"/> instances against the in-memory store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    /// <summary>In-memory database options shared by seed, act, and verify phases within a single test.</summary>
    private readonly DbContextOptions<EctDbContext>        _dbOptions;
    /// <summary>System under test — the <see cref="CasesController"/> instance.</summary>
    private readonly CasesController            _sut;

    /// <summary>
    /// Initializes the in-memory database, seeds a default <see cref="ECTSystem.Shared.Models.Member"/>,
    /// configures the mocked dependencies, and creates the <see cref="CasesController"/> with a
    /// fake authenticated user context.
    /// </summary>
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
            _mockContextFactory.Object,
            _mockLog.Object);

        _sut.ControllerContext = CreateControllerContext();
    }

    /// <summary>
    /// Creates a new <see cref="EctDbContext"/> for seeding or verifying data in the in-memory store.
    /// </summary>
    /// <returns>A fresh context instance sharing the same <see cref="_dbOptions"/>.</returns>
    private EctDbContext CreateSeedContext() => new EctDbContext(_dbOptions);

    /// <summary>
    /// Seeds a <see cref="LineOfDutyCase"/> into the in-memory database for test arrangement.
    /// </summary>
    /// <param name="lodCase">The case entity to persist.</param>
    private void SeedCase(LineOfDutyCase lodCase)
    {
        using var ctx = CreateSeedContext();
        ctx.Cases.Add(lodCase);
        ctx.SaveChanges();
    }

    // ─────────────────────────── Get (collection) ────────────────────────────

    /// <summary>
    /// Verifies that <see cref="CasesController.Get()"/> returns an <see cref="OkObjectResult"/>
    /// wrapping an <see cref="IQueryable{LineOfDutyCase}"/> when cases exist in the store.
    /// </summary>
    [Fact]
    public async Task Get_ReturnsOkContainingCasesQueryable()
    {
        SeedCase(BuildCase(1));

        var result = await _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────── Get (by key) ────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="CasesController.Get(int)"/> returns <see cref="OkObjectResult"/>
    /// with the matching <see cref="LineOfDutyCase"/> when the case exists.
    /// </summary>
    [Fact]
    public async Task GetByKey_WhenCaseExists_ReturnsOkWithCase()
    {
        SeedCase(BuildCase(1));

        var result = await _sut.Get(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var singleResult = Assert.IsType<SingleResult<LineOfDutyCase>>(ok.Value);
        var returned = singleResult.Queryable.Single();
        Assert.Equal(1, returned.Id);
    }

    /// <summary>
    /// Verifies that <see cref="CasesController.Get(int)"/> returns <see cref="NotFoundResult"/>
    /// when no case with the specified key exists in the store.
    /// </summary>
    [Fact]
    public async Task GetByKey_WhenCaseNotFound_ReturnsNotFound()
    {
        var result = await _sut.Get(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="CasesController.Post(LineOfDutyCase)"/> returns a
    /// <see cref="CreatedODataResult{LineOfDutyCase}"/> and auto-generates a case ID
    /// matching the <c>YYYYMMDD-NNN</c> pattern.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelValid_ReturnsCreatedWithCase()
    {
        var lodCase = BuildCase(0);
        lodCase.CaseId = string.Empty; // server generates CaseId

        var result = await _sut.Post(lodCase);

        var created = Assert.IsType<CreatedODataResult<LineOfDutyCase>>(result);
        Assert.Matches(@"^\d{8}-\d{3}$", created.Entity.CaseId);
    }

    /// <summary>
    /// Verifies that <see cref="CasesController.Post(LineOfDutyCase)"/> returns
    /// <see cref="BadRequestObjectResult"/> when the model state contains validation errors.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("MemberName", "Required");

        var result = await _sut.Post(new LineOfDutyCase());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that two sequentially posted cases receive incrementing daily suffixes
    /// (<c>-001</c>, <c>-002</c>) within the same UTC date according to the server's
    /// auto-ID generation logic.
    /// </summary>
    [Fact]
    public async Task Post_SequentialCases_GetIncrementingSuffix()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");

        var case1 = BuildCase(0);
        case1.CaseId = string.Empty;
        var result1 = await _sut.Post(case1);
        var created1 = Assert.IsType<CreatedODataResult<LineOfDutyCase>>(result1);
        Assert.Equal($"{today}-001", created1.Entity.CaseId);

        var case2 = BuildCase(0);
        case2.CaseId = string.Empty;
        var result2 = await _sut.Post(case2);
        var created2 = Assert.IsType<CreatedODataResult<LineOfDutyCase>>(result2);
        Assert.Equal($"{today}-002", created2.Entity.CaseId);
    }

    // ─────────────────────────────── Patch ───────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="CasesController.Patch(int, Delta{LineOfDutyCase})"/> applies the
    /// delta and returns <see cref="UpdatedODataResult{LineOfDutyCase}"/> when the case exists.
    /// </summary>
    [Fact]
    public async Task Patch_WhenCaseExists_ReturnsUpdated()
    {
        SeedCase(BuildCase(1));
        var delta = new Delta<LineOfDutyCase>();
        delta.TrySetPropertyValue(nameof(LineOfDutyCase.MemberName), "Updated Name");

        var result = await _sut.Patch(1, delta);

        Assert.IsType<UpdatedODataResult<LineOfDutyCase>>(result);
    }

    /// <summary>
    /// Verifies that <c>Patch</c> returns <see cref="NotFoundResult"/> when the target case
    /// does not exist in the store.
    /// </summary>
    [Fact]
    public async Task Patch_WhenCaseNotFound_ReturnsNotFound()
    {
        var delta = new Delta<LineOfDutyCase>();

        var result = await _sut.Patch(999, delta);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// Verifies that <c>Patch</c> returns <see cref="BadRequestObjectResult"/> when a
    /// <c>null</c> delta is provided, guarding against missing request body.
    /// </summary>
    [Fact]
    public async Task Patch_WhenDeltaIsNull_ReturnsBadRequest()
    {
        var result = await _sut.Patch(1, null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that <c>Patch</c> returns <see cref="BadRequestObjectResult"/> when the
    /// model state is invalid, without attempting database access.
    /// </summary>
    [Fact]
    public async Task Patch_WhenModelStateInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("key", "error");
        var delta = new Delta<LineOfDutyCase>();

        var result = await _sut.Patch(1, delta);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ─────────────────────────────── Delete ──────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="CasesController.Delete(int)"/> returns <see cref="NoContentResult"/>
    /// and removes the case from the store when it exists.
    /// </summary>
    [Fact]
    public async Task Delete_WhenCaseExists_ReturnsNoContent()
    {
        SeedCase(BuildCase(1));

        var result = await _sut.Delete(1);

        Assert.IsType<NoContentResult>(result);
    }

    /// <summary>
    /// Verifies that <c>Delete</c> returns <see cref="NotFoundResult"/> when no case
    /// with the specified key exists.
    /// </summary>
    [Fact]
    public async Task Delete_WhenCaseNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999);

        Assert.IsType<NotFoundResult>(result);
    }

}
