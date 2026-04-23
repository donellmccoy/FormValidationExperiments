using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Respawn;
using Testcontainers.MsSql;
using Xunit;

namespace ECTSystem.Tests.Performance;

/// <summary>
/// Demonstrates <see cref="Respawner"/> for fast, reliable database cleanup between tests.
/// Respawn uses intelligent DELETE statements (respecting FK order) instead of
/// DROP/CREATE, making it significantly faster than <c>EnsureDeleted()</c>/<c>EnsureCreated()</c>.
/// <para>
/// <b>Requires Docker to be running.</b> Respawn only supports SQL Server, PostgreSQL, and MySQL.
/// These tests use Testcontainers SQL Server for a real database.
/// </para>
/// <para>
/// Run selectively: dotnet test --filter "FullyQualifiedName~RespawnTests"
/// </para>
/// </summary>
[Trait("Category", "Testcontainers")]
public class RespawnTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private MsSqlContainer _container;
    private DbContextOptions<EctDbContext> _dbOptions;
    private Respawner _respawner;

    public RespawnTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask InitializeAsync()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync();

        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        await using var context = new EctDbContext(_dbOptions);
        await context.Database.EnsureCreatedAsync();

        using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Respawn_CleansDataBetweenTests()
    {
        await using var context = new EctDbContext(_dbOptions);

        // Seed some data
        context.Members.Add(new Member
        {
            FirstName = "Respawn",
            LastName = "Test",
            Rank = "A1C",
            Component = ServiceComponent.RegularAirForce,
            ServiceNumber = "1111111111"
        });
        await context.SaveChangesAsync();

        var beforeCount = await context.Members.CountAsync();
        Assert.True(beforeCount > 0, "Expected seeded data");

        // Reset the database using Respawn
        using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);

        // Verify cleanup
        var afterCount = await context.Members.CountAsync();
        _output.WriteLine($"Members before reset: {beforeCount}, after: {afterCount}");
        Assert.Equal(0, afterCount);
    }

    [Fact]
    public async Task Respawn_HandlesRelatedEntities()
    {
        await using var context = new EctDbContext(_dbOptions);

        // Seed a case with related entities
        var member = new Member
        {
            FirstName = "Cascade",
            LastName = "Test",
            Rank = "SSgt",
            Component = ServiceComponent.AirForceReserve,
            ServiceNumber = "2222222222"
        };
        context.Members.Add(member);
        await context.SaveChangesAsync();

        var lodCase = new LineOfDutyCase
        {
            CaseId = "RESPAWN-001",
            MemberName = "Test, Cascade",
            MemberRank = "SSgt",
            MemberId = member.Id,
            ProcessType = ProcessType.Informal,
            Component = ServiceComponent.AirForceReserve,
            IncidentType = IncidentType.Injury,
            IncidentDate = DateTime.UtcNow,
            MEDCON = new MEDCONDetail(),
            INCAP = new INCAPDetails(),
            Authorities = new List<LineOfDutyAuthority>
            {
                new() { Role = "Immediate Commander", Name = "Maj Respawn Test" }
            }
        };
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync();

        // Reset
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);

        // Verify all related entities are cleaned
        Assert.Equal(0, await context.Cases.CountAsync());
        Assert.Equal(0, await context.Members.CountAsync());

        _output.WriteLine("Respawn successfully cleaned cases, members, and related entities");
    }
}
