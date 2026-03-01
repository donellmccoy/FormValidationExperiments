using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

/// <summary>
/// Controller unit tests for <see cref="MembersController"/>.
/// Uses an in-memory EF Core database (same pattern as DataServiceTestBase) because
/// MembersController injects IDbContextFactory directly rather than a service interface.
/// </summary>
public class MembersControllerTests : ControllerTestBase
{
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockFactory;
    private readonly Mock<ILoggingService>  _mockLog;
    private readonly MembersController    _sut;

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

    private EctDbContext CreateSeedContext() => new EctDbContext(_dbOptions);

    private static Member BuildMember(int id = 0, string firstName = "John", string lastName = "Doe") => new Member
    {
        Id        = id,
        FirstName = firstName,
        LastName  = lastName,
        Rank      = "SSgt",
        Unit      = "99 ABW"
    };

    // ─────────────────────────── Get (collection) ────────────────────────────

    [Fact]
    public void Get_ReturnsOkWithMembersQueryable()
    {
        var result = _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ─────────────────────────── Get (by key) ────────────────────────────────

    [Fact]
    public async Task GetByKey_WhenMemberExists_ReturnsOkWithMember()
    {
        await using var ctx = CreateSeedContext();
        ctx.Members.Add(BuildMember(1));
        await ctx.SaveChangesAsync();

        var result = await _sut.Get(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var member = Assert.IsType<Member>(ok.Value);
        Assert.Equal(1, member.Id);
    }

    [Fact]
    public async Task GetByKey_WhenMemberNotFound_ReturnsNotFound()
    {
        var result = await _sut.Get(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    [Fact]
    public async Task Post_WhenModelValid_PersistsMemberAndReturnsCreated()
    {
        var member = BuildMember(0, "New", "Recruit");

        var result = await _sut.Post(member);

        Assert.IsType<CreatedODataResult<Member>>(result);

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.Members.AnyAsync(m => m.LastName == "Recruit"));
    }

    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("FirstName", "Required");

        var result = await _sut.Post(new Member());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ─────────────────────────────── Put ─────────────────────────────────────

    [Fact]
    public async Task Put_WhenMemberExists_UpdatesMemberAndReturnsUpdated()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Members.Add(BuildMember(1, "John", "Doe"));
        await seedCtx.SaveChangesAsync();

        var update = new Member { FirstName = "Jane", LastName = "Smith", Rank = "TSgt", Unit = "12 OG" };

        var result = await _sut.Put(1, update);

        Assert.IsType<UpdatedODataResult<Member>>(result);

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Members.FindAsync(1);
        Assert.Equal("Jane",  saved.FirstName);
        Assert.Equal("Smith", saved.LastName);
        Assert.Equal("TSgt",  saved.Rank);
    }

    [Fact]
    public async Task Put_WhenMemberNotFound_ReturnsNotFound()
    {
        var result = await _sut.Put(999, new Member { FirstName = "Ghost" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Put_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("FirstName", "Required");

        var result = await _sut.Put(1, new Member());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ─────────────────────────────── Patch ───────────────────────────────────

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

    [Fact]
    public async Task Patch_WhenMemberNotFound_ReturnsNotFound()
    {
        var delta = new Delta<Member>();
        delta.TrySetPropertyValue(nameof(Member.FirstName), "Ghost");

        var result = await _sut.Patch(999, delta);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Delete ──────────────────────────────────

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

    [Fact]
    public async Task Delete_WhenMemberNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999);

        Assert.IsType<NotFoundResult>(result);
    }
}
