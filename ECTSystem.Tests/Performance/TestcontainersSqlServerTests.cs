using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Testcontainers.MsSql;
using Xunit;

namespace ECTSystem.Tests.Performance;

/// <summary>
/// Integration tests using Testcontainers for SQL Server to run tests against a real
/// SQL Server instance in Docker. This catches SQL Server-specific issues that SQLite
/// cannot reproduce (e.g., locking, concurrency, stored procedures, raw SQL).
/// <para>
/// <b>Requires Docker to be running.</b> These tests are skipped in environments
/// without Docker (CI can enable via <c>DOCKER_AVAILABLE=true</c> environment variable).
/// </para>
/// <para>
/// Run selectively: dotnet test --filter "FullyQualifiedName~TestcontainersSqlServer"
/// </para>
/// </summary>
[Trait("Category", "Testcontainers")]
public class TestcontainersSqlServerTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private MsSqlContainer _container;
    private DbContextOptions<EctDbContext> _dbOptions;

    public TestcontainersSqlServerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask InitializeAsync()
    {
        // Start a SQL Server container
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync(TestContext.Current.CancellationToken);

        _output.WriteLine($"SQL Server container started: {_container.GetConnectionString()}");

        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        // Create the schema
        await using var context = new EctDbContext(_dbOptions);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _output.WriteLine("Database schema created");
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task CanCreateAndQueryCase_RealSqlServer()
    {
        await using var context = new EctDbContext(_dbOptions);

        var member = new Member
        {
            FirstName = "Container",
            LastName = "Test",
            Rank = "TSgt",
            Component = ServiceComponent.RegularAirForce,
            ServiceNumber = "1234567890"
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var lodCase = new LineOfDutyCase
        {
            CaseId = "TC-20250315-001",
            MemberName = "Test, Container A.",
            MemberRank = "TSgt",
            MemberId = member.Id,
            ProcessType = ProcessType.Informal,
            Component = ServiceComponent.RegularAirForce,
            IncidentType = IncidentType.Injury,
            IncidentDutyStatus = DutyStatus.Title10ActiveDuty,
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            IncidentDescription = "Testcontainers integration test",
            MEDCON = new MEDCONDetail(),
            INCAP = new INCAPDetails()
        };
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var retrieved = await context.Cases
            .Include(c => c.Member)
            .FirstOrDefaultAsync(c => c.CaseId == "TC-20250315-001", TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("Test, Container A.", retrieved.MemberName);
        Assert.Equal(member.Id, retrieved.MemberId);

        _output.WriteLine($"Case {retrieved.CaseId} created and retrieved from SQL Server container");
    }

    [Fact]
    public async Task ConcurrentInserts_DoNotDeadlock()
    {
        var tasks = Enumerable.Range(1, 10).Select(async i =>
        {
            await using var context = new EctDbContext(_dbOptions);

            var member = new Member
            {
                FirstName = $"Concurrent{i}",
                LastName = "Test",
                Rank = "A1C",
                Component = ServiceComponent.AirForceReserve,
                ServiceNumber = $"{i:D10}"
            };
            context.Members.Add(member);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var lodCase = new LineOfDutyCase
            {
                CaseId = $"TC-CONC-{i:D3}",
                MemberName = $"Test, Concurrent{i}",
                MemberRank = "A1C",
                MemberId = member.Id,
                ProcessType = ProcessType.Informal,
                Component = ServiceComponent.AirForceReserve,
                IncidentType = IncidentType.Injury,
                IncidentDate = DateTime.UtcNow,
                IncidentDescription = $"Concurrent test {i}",
                MEDCON = new MEDCONDetail(),
                INCAP = new INCAPDetails()
            };
            context.Cases.Add(lodCase);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await Task.WhenAll(tasks);

        await using var verifyContext = new EctDbContext(_dbOptions);
        var count = await verifyContext.Cases.CountAsync(c => c.CaseId.StartsWith("TC-CONC-"), TestContext.Current.CancellationToken);

        _output.WriteLine($"Concurrent inserts: {count}/10 cases created without deadlock");
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task RowVersionConcurrency_DetectsConflict()
    {
        await using var setupContext = new EctDbContext(_dbOptions);

        var member = new Member
        {
            FirstName = "Concurrency",
            LastName = "Test",
            Rank = "MSgt",
            Component = ServiceComponent.RegularAirForce,
            ServiceNumber = "5555555555"
        };
        setupContext.Members.Add(member);
        await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var lodCase = new LineOfDutyCase
        {
            CaseId = "TC-CONCUR-001",
            MemberName = "Test, Concurrency",
            MemberRank = "MSgt",
            MemberId = member.Id,
            ProcessType = ProcessType.Informal,
            Component = ServiceComponent.RegularAirForce,
            IncidentType = IncidentType.Injury,
            IncidentDate = DateTime.UtcNow,
            IncidentDescription = "Original description",
            MEDCON = new MEDCONDetail(),
            INCAP = new INCAPDetails()
        };
        setupContext.Cases.Add(lodCase);
        await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Simulate two concurrent editors
        await using var context1 = new EctDbContext(_dbOptions);
        await using var context2 = new EctDbContext(_dbOptions);

        var case1 = await context1.Cases.FirstAsync(c => c.CaseId == "TC-CONCUR-001", TestContext.Current.CancellationToken);
        var case2 = await context2.Cases.FirstAsync(c => c.CaseId == "TC-CONCUR-001", TestContext.Current.CancellationToken);

        // First update succeeds
        case1.IncidentDescription = "Updated by user 1";
        await context1.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Second update should fail with concurrency exception
        case2.IncidentDescription = "Updated by user 2";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => context2.SaveChangesAsync(TestContext.Current.CancellationToken));

        _output.WriteLine("RowVersion concurrency conflict correctly detected on SQL Server");
    }
}
