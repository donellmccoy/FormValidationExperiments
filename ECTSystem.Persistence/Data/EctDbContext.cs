using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data;

/// <summary>
/// Entity Framework Core database context for the ECT (LOD) application domain model.
/// Identity is handled separately by <see cref="EctIdentityDbContext"/>.
/// </summary>
public class EctDbContext : DbContext
{
    public EctDbContext(DbContextOptions<EctDbContext> options) : base(options)
    {
    }

    public DbSet<LineOfDutyCase> Cases { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<LineOfDutyDocument> Documents { get; set; }
    public DbSet<LineOfDutyAppeal> Appeals { get; set; }
    public DbSet<LineOfDutyAuthority> Authorities { get; set; }
    public DbSet<MEDCONDetail> MEDCONDetails { get; set; }
    public DbSet<INCAPDetails> INCAPDetails { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Bookmark> Bookmarks { get; set; }
    public DbSet<WorkflowStateLookup> WorkflowStates { get; set; }
    public DbSet<WorkflowStateHistory> WorkflowStateHistories { get; set; }
    public DbSet<WitnessStatement> WitnessStatements { get; set; }
    public DbSet<AuditComment> AuditComments { get; set; }
    public DbSet<WorkflowType> WorkflowTypes { get; set; }
    public DbSet<WorkflowModule> WorkflowModules { get; set; }

    // Audit fields are populated by AuditSaveChangesInterceptor (registered via DI).
    // The interceptor reads the current user from IHttpContextAccessor and sets
    // CreatedBy/ModifiedBy/CreatedDate/ModifiedDate on AuditableEntity entries.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EctDbContext).Assembly);
        modelBuilder.DisableDatabaseCascadeDelete();
    }
}
