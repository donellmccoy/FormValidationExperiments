using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
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
/// Unit tests for <see cref="AuthoritiesController"/>, the OData controller managing
/// LineOfDutyAuthority CRUD operations.
/// </summary>
public class AuthoritiesControllerTests : ControllerTestBase
{
    private readonly Mock<ILoggingService> _mockLog;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    private readonly AuthoritiesController _sut;

    public AuthoritiesControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        // Seed a default Member and Case so FK references are valid
        using (var seedCtx = new EctDbContext(_dbOptions))
        {
            seedCtx.Members.Add(new Member
            {
                Id = 1, FirstName = "John", LastName = "Doe",
                Rank = "SSgt", Unit = "99 ABW"
            });
            seedCtx.SaveChanges();

            seedCtx.Cases.Add(BuildCase(1));
            seedCtx.SaveChanges();
        }

        _mockLog = new Mock<ILoggingService>();
        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();

        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));

        _sut = new AuthoritiesController(_mockContextFactory.Object, _mockLog.Object, TimeProvider.System);
        _sut.ControllerContext = CreateControllerContext();
    }

    private EctDbContext CreateSeedContext() => new EctDbContext(_dbOptions);

    private void SeedAuthority(LineOfDutyAuthority authority)
    {
        using var ctx = CreateSeedContext();
        ctx.Authorities.Add(authority);
        ctx.SaveChanges();
    }

    // ────────────────────────── Post ──────────────────────────────

    [Fact]
    public async Task Post_InsertsNewAuthority()
    {
        var dto = new CreateAuthorityDto
        {
            Role = "Commander", Name = "Smith, John", Rank = "Col",
            LineOfDutyCaseId = 1
        };

        var result = await _sut.Post(dto, TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedODataResult<LineOfDutyAuthority>>(result);
        Assert.Equal("Commander", created.Entity.Role);
        Assert.Equal(1, created.Entity.LineOfDutyCaseId);
    }

    [Fact]
    public async Task Post_WhenNoCaseId_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("LineOfDutyCaseId", "The field LineOfDutyCaseId must be between 1 and 2147483647.");

        var dto = new CreateAuthorityDto
        {
            Role = "Commander", Name = "Smith, John", Rank = "Col"
        };

        var result = await _sut.Post(dto, TestContext.Current.CancellationToken);

        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(obj.Value);
        Assert.NotEmpty(problem.Errors);
    }

    // ────────────────────────── Get ──────────────────────────────

    [Fact]
    public async Task Get_ByKey_ReturnsAuthority()
    {
        SeedAuthority(new LineOfDutyAuthority
        {
            Role = "SJA", Name = "Jones, Jane", Rank = "Maj",
            LineOfDutyCaseId = 1
        });

        using var ctx = CreateSeedContext();
        var seeded = await ctx.Authorities.FirstAsync(TestContext.Current.CancellationToken);

        var result = await _sut.Get(seeded.Id, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Get_ByKey_WhenNotFound_ReturnsNotFound()
    {
        var result = await _sut.Get(999, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var singleResult = Assert.IsType<SingleResult<LineOfDutyAuthority>>(ok.Value);
        Assert.False(singleResult.Queryable.Any());
    }

    // ────────────────────────── Patch ──────────────────────────────

    [Fact]
    public async Task Patch_UpdatesExistingAuthority()
    {
        SeedAuthority(new LineOfDutyAuthority
        {
            Role = "Commander", Name = "Old Name", Rank = "Col",
            LineOfDutyCaseId = 1
        });

        using var ctx = CreateSeedContext();
        var seeded = await ctx.Authorities.FirstAsync(TestContext.Current.CancellationToken);

        var delta = new Delta<LineOfDutyAuthority>();
        delta.TrySetPropertyValue(nameof(LineOfDutyAuthority.Name), "New Name");
        delta.TrySetPropertyValue(nameof(LineOfDutyAuthority.Rank), "BGen");

        var result = await _sut.Patch(seeded.Id, delta, TestContext.Current.CancellationToken);

        var updated = Assert.IsType<UpdatedODataResult<LineOfDutyAuthority>>(result);
        Assert.Equal("New Name", updated.Entity.Name);
        Assert.Equal("BGen", updated.Entity.Rank);
    }

    [Fact]
    public async Task Patch_WhenNotFound_ReturnsNotFound()
    {
        var delta = new Delta<LineOfDutyAuthority>();
        delta.TrySetPropertyValue(nameof(LineOfDutyAuthority.Name), "New Name");

        var result = await _sut.Patch(999, delta, TestContext.Current.CancellationToken);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
    }

    // ────────────────────────── Delete ──────────────────────────────

    [Fact]
    public async Task Delete_RemovesAuthority()
    {
        SeedAuthority(new LineOfDutyAuthority
        {
            Role = "Commander", Name = "Smith, John", Rank = "Col",
            LineOfDutyCaseId = 1
        });

        using var ctx = CreateSeedContext();
        var seeded = await ctx.Authorities.FirstAsync(TestContext.Current.CancellationToken);

        var result = await _sut.Delete(seeded.Id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);

        using var verifyCtx = CreateSeedContext();
        Assert.Empty(await verifyCtx.Authorities.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999, TestContext.Current.CancellationToken);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
    }

    // ────────────────────────── Authorization regression ──────────────────────────────

    [Theory]
    [InlineData(nameof(AuthoritiesController.Post), new[] { typeof(CreateAuthorityDto), typeof(CancellationToken) })]
    [InlineData(nameof(AuthoritiesController.Patch), new[] { typeof(int), typeof(Delta<LineOfDutyAuthority>), typeof(CancellationToken) })]
    [InlineData(nameof(AuthoritiesController.Delete), new[] { typeof(int), typeof(CancellationToken) })]
    public void AuthorityWriteEndpoints_RequireAdminRole(string methodName, Type[] parameterTypes)
    {
        var method = typeof(AuthoritiesController).GetMethod(methodName, parameterTypes);
        Assert.NotNull(method);

        var attr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("Admin", attr!.Roles);
    }
}
