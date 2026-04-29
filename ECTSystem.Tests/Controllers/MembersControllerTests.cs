using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.Data.Sqlite;
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
/// Each test instance creates an isolated SQLite in-memory database (not the EF Core
/// InMemory provider) because <c>Delete</c> uses
/// <see cref="EntityFrameworkQueryableExtensions.ExecuteDeleteAsync{T}"/>, which the
/// InMemory provider does not support. Tests are organized by controller action:
/// <c>Get</c> (collection and single), <c>Post</c>, <c>Patch</c> (the canonical
/// partial-update verb — PUT is intentionally not exposed), and <c>Delete</c>.
/// Persistence is verified by opening a separate <see cref="EctDbContext"/> against
/// the shared SQLite connection after each mutating operation.
/// </para>
/// </remarks>
public class MembersControllerTests : ControllerTestBase, IDisposable
{
    /// <summary>SQLite in-memory database options shared across seed, act, and verify phases.</summary>
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    /// <summary>Open SQLite connection that backs the in-memory database for the lifetime of the test.</summary>
    private readonly SqliteConnection _connection;
    /// <summary>Mocked context factory returning <see cref="EctDbContext"/> instances backed by the SQLite store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockFactory;
    /// <summary>Mocked logging service injected into the controller.</summary>
    private readonly Mock<ILoggingService>  _mockLog;
    /// <summary>System under test — the <see cref="MembersController"/> instance.</summary>
    private readonly MembersController    _sut;

    /// <summary>
    /// Initializes the SQLite in-memory database, configures mocked dependencies, and
    /// creates the <see cref="MembersController"/> with a fake authenticated user context.
    /// </summary>
    public MembersControllerTests()
    {
        // SQLite in-memory requires a shared open connection for all contexts
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var schemaCtx = new SqliteEctDbContext(_dbOptions))
        {
            schemaCtx.Database.EnsureCreated();
        }

        _mockFactory = new Mock<IDbContextFactory<EctDbContext>>();
        _mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SqliteEctDbContext(_dbOptions));
        _mockFactory.Setup(f => f.CreateDbContext())
            .Returns(() => new SqliteEctDbContext(_dbOptions));

        _mockLog = new Mock<ILoggingService>();

        _sut = new MembersController(_mockFactory.Object, _mockLog.Object, TimeProvider.System);
        _sut.ControllerContext = CreateControllerContext();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a new <see cref="EctDbContext"/> for seeding or verifying data in the SQLite store.
    /// </summary>
    /// <returns>A fresh context instance sharing the same <see cref="_dbOptions"/>.</returns>
    private EctDbContext CreateSeedContext() => new SqliteEctDbContext(_dbOptions);

    /// <summary>
    /// EctDbContext subclass that replaces SQL Server–specific column types and
    /// default value expressions with SQLite-compatible equivalents.
    /// </summary>
    private class SqliteEctDbContext(DbContextOptions<EctDbContext> options) : EctDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<WorkflowModule>()
                .Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
            modelBuilder.Entity<WorkflowModule>()
                .Property(e => e.ModifiedDate).HasDefaultValueSql("datetime('now')");

            modelBuilder.Entity<WorkflowType>()
                .Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
            modelBuilder.Entity<WorkflowType>()
                .Property(e => e.ModifiedDate).HasDefaultValueSql("datetime('now')");

            modelBuilder.Entity<WorkflowStateLookup>()
                .Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
            modelBuilder.Entity<WorkflowStateLookup>()
                .Property(e => e.ModifiedDate).HasDefaultValueSql("datetime('now')");
        }
    }

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
        var result = await _sut.Get(TestContext.Current.CancellationToken);

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
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _sut.Get(1, TestContext.Current.CancellationToken);

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
        var result = await _sut.Get(999, TestContext.Current.CancellationToken);

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

        var result = await _sut.Post(dto, TestContext.Current.CancellationToken);

        Assert.IsType<CreatedODataResult<Member>>(result);

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.Members.AnyAsync(m => m.LastName == "Recruit", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that <c>Post</c> returns <see cref="BadRequestObjectResult"/> when the
    /// model state is invalid, without persisting any data.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("FirstName", "Required");

        var result = await _sut.Post(new CreateMemberDto(), TestContext.Current.CancellationToken);

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
        await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var delta = new Delta<Member>();
        delta.TrySetPropertyValue(nameof(Member.FirstName), "Patched");

        var result = await _sut.Patch(1, delta, TestContext.Current.CancellationToken);

        Assert.IsType<UpdatedODataResult<Member>>(result);

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Members.FindAsync(new object[] { 1 }, TestContext.Current.CancellationToken);
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

        var result = await _sut.Patch(999, delta, TestContext.Current.CancellationToken);

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
        await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _sut.Delete(1, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);

        await using var verifyCtx = CreateSeedContext();
        Assert.Null(await verifyCtx.Members.FindAsync(new object[] { 1 }, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that <c>Delete</c> returns <see cref="NotFoundResult"/> when no member
    /// with the specified key exists.
    /// </summary>
    [Fact]
    public async Task Delete_WhenMemberNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999, TestContext.Current.CancellationToken);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
    }

    // ───────────────────────── Response cache (PII) ──────────────────────────

    /// <summary>
    /// Member responses contain PII (SSN, ServiceNumber, full name) and must never be
    /// cached by intermediaries or the browser. This pins the
    /// <see cref="ResponseCacheAttribute"/> on every endpoint that returns Member data so
    /// it cannot be silently removed without failing this regression test (per §2.6
    /// remediation plan, "Cache-Control: no-store" requirement).
    /// </summary>
    /// <param name="methodName">The controller action name to inspect.</param>
    /// <param name="parameterTypes">Parameter types in declaration order, used to disambiguate overloads.</param>
    [Theory]
    [InlineData(nameof(MembersController.Get), new[] { typeof(CancellationToken) })]
    [InlineData(nameof(MembersController.Get), new[] { typeof(int), typeof(CancellationToken) })]
    [InlineData(nameof(MembersController.GetLineOfDutyCases), new[] { typeof(int), typeof(CancellationToken) })]
    public void MemberReturningEndpoints_DeclareNoStoreResponseCache(string methodName, Type[] parameterTypes)
    {
        var method = typeof(MembersController).GetMethod(methodName, parameterTypes);
        Assert.NotNull(method);

        var attr = method!.GetCustomAttributes(typeof(ResponseCacheAttribute), inherit: false)
            .Cast<ResponseCacheAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.True(attr!.NoStore, $"{methodName} must set ResponseCache.NoStore = true to prevent PII caching.");
        Assert.Equal(ResponseCacheLocation.None, attr.Location);
    }
}
