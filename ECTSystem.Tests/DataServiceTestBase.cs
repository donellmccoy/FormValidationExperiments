using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Tests;

/// <summary>
/// Base class for DataService tests. Each test class instance gets its own
/// isolated in-memory database, ensuring test independence.
/// </summary>
public abstract class DataServiceTestBase
{
    protected readonly DbContextOptions<EctDbContext> DbOptions;
    protected readonly Mock<IDbContextFactory<EctDbContext>> MockFactory;
    protected readonly DataService Sut;

    protected const int DefaultMemberId = 1;

    protected DataServiceTestBase()
    {
        DbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        // Seed a default Member so that required FK references are valid
        // when querying with Include(c => c.Member) on the InMemory provider.
        using (var seedCtx = new EctDbContext(DbOptions))
        {
            seedCtx.Members.Add(new Member
            {
                Id = DefaultMemberId,
                FirstName = "John",
                LastName = "Doe",
                Rank = "SSgt",
                Unit = "99 ABW"
            });
            seedCtx.SaveChanges();
        }

        MockFactory = new Mock<IDbContextFactory<EctDbContext>>();

        MockFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(DbOptions));

        MockFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(DbOptions));

        Sut = new DataService(MockFactory.Object);
    }

    /// <summary>
    /// Creates a fresh context on the shared in-memory database for seeding test data.
    /// </summary>
    protected EctDbContext CreateSeedContext() => new EctDbContext(DbOptions);

    /// <summary>
    /// Creates a minimal valid LineOfDutyCase suitable for seeding.
    /// </summary>
    protected static LineOfDutyCase BuildCase(int id = 1, string caseId = null) => new LineOfDutyCase
    {
        Id = id,
        CaseId = caseId ?? $"CASE-{id:D4}",
        MemberId = DefaultMemberId,
        MemberName = "SSgt John Doe",
        MemberRank = "SSgt",
        Unit = "99 ABW",
        IncidentDescription = "Training injury",
        InitiationDate = new DateTime(2025, 1, 15),
        MEDCON = new MEDCONDetail(),
        INCAP = new INCAPDetails(),
        Authorities = [],
        Documents = [],
        Appeals = [],
        TimelineSteps = [],
        Notifications = [],
        WitnessStatements = [],
        AuditComments = []
    };
}
