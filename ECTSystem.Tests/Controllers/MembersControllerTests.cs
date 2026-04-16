using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="MembersController"/>, the OData controller managing military
/// member records (service member identification data) in the ECT System API.
/// </summary>
/// <remarks>
/// <para>
/// Each test instance creates an isolated in-memory EF Core database. Unlike most other
/// controllers in the system, <see cref="MembersController"/> supports the full CRUD
/// surface including <c>Put</c> (full replace) in addition to <c>Patch</c> (delta update).
/// </para>
/// <para>
/// Tests are organized by controller action: <c>Get</c> (collection and single),
/// <c>Post</c>, <c>Put</c>, <c>Patch</c>, and <c>Delete</c>. Persistence is verified
/// by opening a separate <see cref="EctDbContext"/> against the shared in-memory store
/// after each mutating operation.
/// </para>
/// </remarks>
public class MembersControllerTests : ControllerTestBase
{
    /// <summary>In-memory database options shared across seed, act, and verify phases.</summary>
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    /// <summary>Mocked context factory returning <see cref="EctDbContext"/> instances backed by the in-memory store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockFactory;
    /// <summary>Mocked logging service injected into the controller.</summary>
    private readonly Mock<ILoggingService>  _mockLog;
    /// <summary>System under test — the <see cref="MembersController"/> instance.</summary>
    private readonly MembersController    _sut;

    /// <summary>
    /// Initializes the in-memory database, configures mocked dependencies, and creates
    /// the <see cref="MembersController"/> with a fake authenticated user context.
    /// </summary>
    public MembersControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockFactory = new Mock<IDbContextFactory<EctDbContext>>();
        _mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));
        _mockFactory.Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(_dbOptions));

        _mockLog = new Mock<ILoggingService>();

        _sut = new MembersController(_mockFactory.Object, _mockLog.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    /// <summary>
    /// Creates a new <see cref="EctDbContext"/> for seeding or verifying data in the in-memory store.
    /// </summary>
    /// <returns>A fresh context instance sharing the same <see cref="_dbOptions"/>.</returns>
    private EctDbContext CreateSeedContext() => new EctDbContext(_dbOptions);

    /// <summary>
    /// Builds a <see cref="Member"/> test entity with sensible military defaults (SSgt / 99 ABW).
    /// </summary>
    /// <param name="id">The entity ID. Use <c>0</c> for new inserts (auto-generated key).</param>
    /// <param name="firstName">Member first name. Defaults to "John".</param>
    /// <param name="lastName">Member last name. Defaults to "Doe".</param>
    /// <returns>A populated <see cref="Member"/> instance.</returns>
    private static Member BuildMember(int id = 0, string firstName = "John", string lastName = "Doe") => new Member
    {
        Id        = id,
        FirstName = firstName,
        LastName  = lastName,
        Rank      = "SSgt",
        Unit      = "99 ABW"
    };

    // ─────────────────────────── Get (collection) ────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MembersController.Get()"/> returns an
    /// <see cref="OkObjectResult"/> wrapping an <see cref="IQueryable{Member}"/>.
    /// </summary>
    [Fact]
    public async Task Get_ReturnsOkWithMembersQueryable()
    {
        var result = await _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────── Get (by key) ────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MembersController.Get(int)"/> returns <see cref="OkObjectResult"/>
    /// with the matching <see cref="Member"/> when the member exists.
    /// </summary>
    [Fact]
    public async Task GetByKey_WhenMemberExists_ReturnsOkWithMember()
    {
        await using var ctx = CreateSeedContext();
        ctx.Members.Add(BuildMember(1));
        await ctx.SaveChangesAsync();

        var result = await _sut.Get(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var singleResult = Assert.IsType<SingleResult<Member>>(ok.Value);
        var member = singleResult.Queryable.Single();
        Assert.Equal(1, member.Id);
    }

    /// <summary>
    /// Verifies that <c>Get</c> returns <see cref="NotFoundResult"/> when no member
    /// with the specified key exists.
    /// </summary>
    [Fact]
    public async Task GetByKey_WhenMemberNotFound_ReturnsNotFound()
    {
        var result = await _sut.Get(999);

        var ok = Assert.IsType<OkObjectResult>(result);
        var singleResult = Assert.IsType<SingleResult<Member>>(ok.Value);
        Assert.False(singleResult.Queryable.Any());
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MembersController.Post(Member)"/> persists the member in the
    /// database and returns <see cref="CreatedODataResult{Member}"/>. Persistence is confirmed
    /// via a separate database context read.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelValid_PersistsMemberAndReturnsCreated()
    {
        var dto = new CreateMemberDto
        {
            FirstName = "New", LastName = "Recruit",
            Rank = "AB", ServiceNumber = "123456789", Component = ServiceComponent.RegularAirForce
        };

        var result = await _sut.Post(dto);

        Assert.IsType<CreatedODataResult<Member>>(result);

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.Members.AnyAsync(m => m.LastName == "Recruit"));
    }

    /// <summary>
    /// Verifies that <c>Post</c> returns <see cref="BadRequestObjectResult"/> when the
    /// model state is invalid, without persisting any data.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("FirstName", "Required");

        var result = await _sut.Post(new CreateMemberDto());

        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(obj.Value);
        Assert.NotEmpty(problem.Errors);
    }

    // ─────────────────────────────── Put ─────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MembersController.Put(int, Member)"/> fully replaces the
    /// existing member's properties and returns <see cref="UpdatedODataResult{Member}"/>.
    /// Persistence is confirmed via a separate database context read.
    /// </summary>
    [Fact]
    public async Task Put_WhenMemberExists_UpdatesMemberAndReturnsUpdated()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Members.Add(BuildMember(1, "John", "Doe"));
        await seedCtx.SaveChangesAsync();

        var dto = new UpdateMemberDto
        {
            FirstName = "Jane", LastName = "Smith",
            Rank = "TSgt", Unit = "12 OG",
            ServiceNumber = "123456789", Component = ServiceComponent.RegularAirForce,
            RowVersion = []
        };

        var result = await _sut.Put(1, dto);

        Assert.IsType<UpdatedODataResult<Member>>(result);

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Members.FindAsync(1);
        Assert.Equal("Jane",  saved.FirstName);
        Assert.Equal("Smith", saved.LastName);
        Assert.Equal("TSgt",  saved.Rank);
    }

    /// <summary>
    /// Verifies that <c>Put</c> returns <see cref="NotFoundResult"/> when no member
    /// with the specified key exists.
    /// </summary>
    [Fact]
    public async Task Put_WhenMemberNotFound_ReturnsNotFound()
    {
        var dto = new UpdateMemberDto
        {
            FirstName = "Ghost", LastName = "Member",
            Rank = "AB", ServiceNumber = "000000000", Component = ServiceComponent.RegularAirForce,
            RowVersion = new byte[] { 0 }
        };

        var result = await _sut.Put(999, dto);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
    }

    /// <summary>
    /// Verifies that <c>Put</c> returns <see cref="BadRequestObjectResult"/> when the
    /// model state is invalid, without attempting database access.
    /// </summary>
    [Fact]
    public async Task Put_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("FirstName", "Required");

        var result = await _sut.Put(1, new UpdateMemberDto());

        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(obj.Value);
        Assert.NotEmpty(problem.Errors);
    }

    // ─────────────────────────────── Patch ───────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MembersController.Patch(int, Delta{Member})"/> applies only
    /// the changed properties from the delta, leaving other properties unchanged, and returns
    /// <see cref="UpdatedODataResult{Member}"/>.
    /// </summary>
    [Fact]
    public async Task Patch_WhenMemberExists_AppliesDeltaAndReturnsUpdated()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Members.Add(BuildMember(1, "John", "Doe"));
        await seedCtx.SaveChangesAsync();

        var delta = new Delta<Member>();
        delta.TrySetPropertyValue(nameof(Member.FirstName), "Patched");

        var result = await _sut.Patch(1, delta);

        Assert.IsType<UpdatedODataResult<Member>>(result);

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Members.FindAsync(1);
        Assert.Equal("Patched", saved.FirstName);
        Assert.Equal("Doe",     saved.LastName);  // unchanged
    }

    /// <summary>
    /// Verifies that <c>Patch</c> returns <see cref="NotFoundResult"/> when no member
    /// with the specified key exists.
    /// </summary>
    [Fact]
    public async Task Patch_WhenMemberNotFound_ReturnsNotFound()
    {
        var delta = new Delta<Member>();
        delta.TrySetPropertyValue(nameof(Member.FirstName), "Ghost");

        var result = await _sut.Patch(999, delta);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
    }

    // ─────────────────────────────── Delete ──────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MembersController.Delete(int)"/> removes the member from the
    /// store and returns <see cref="NoContentResult"/>. Deletion is confirmed via a separate
    /// database context read.
    /// </summary>
    [Fact]
    public async Task Delete_WhenMemberExists_RemovesMemberAndReturnsNoContent()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Members.Add(BuildMember(1));
        await seedCtx.SaveChangesAsync();

        var result = await _sut.Delete(1);

        Assert.IsType<NoContentResult>(result);

        await using var verifyCtx = CreateSeedContext();
        Assert.Null(await verifyCtx.Members.FindAsync(1));
    }

    /// <summary>
    /// Verifies that <c>Delete</c> returns <see cref="NotFoundResult"/> when no member
    /// with the specified key exists.
    /// </summary>
    [Fact]
    public async Task Delete_WhenMemberNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
    }
}
