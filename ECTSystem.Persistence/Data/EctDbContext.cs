using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data;

/// <summary>
/// Entity Framework Core database context for the ECT (LOD) application domain model.
/// Identity is handled separately by <see cref="EctIdentityDbContext"/>.
/// </summary>
public class EctDbContext : DbContext
{
    public string CurrentUserId { get; set; } = "System";

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
    public DbSet<TimelineStep> TimelineSteps { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<CaseBookmark> CaseBookmarks { get; set; }
    public DbSet<LineOfDutyWorkflowStateLookup> WorkflowStates { get; set; }
    public DbSet<WorkflowStepHistory> WorkflowStepHistories { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = DateTime.UtcNow;
                    entry.Entity.CreatedBy = CurrentUserId;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedDate = DateTime.UtcNow;
                    entry.Entity.ModifiedBy = CurrentUserId;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EctDbContext).Assembly);
    }
}
