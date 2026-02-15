using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Data;

/// <summary>
/// Entity Framework Core database context for the ECT (LOD) application.
/// Configured for SQL Server.
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
    public DbSet<MEDCONDetails> MEDCONDetails { get; set; }
    public DbSet<INCAPDetails> INCAPDetails { get; set; }
    public DbSet<TimelineStep> TimelineSteps { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EctDbContext).Assembly);
    }
}
