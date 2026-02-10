using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Web.Models;

namespace FormValidationExperiments.Web.Data;

/// <summary>
/// Entity Framework Core database context for the Line of Duty application.
/// Configured to use an in-memory database provider.
/// </summary>
public class LODDbContext : DbContext
{
    public LODDbContext(DbContextOptions<LODDbContext> options) : base(options)
    {
    }

    public DbSet<LineOfDutyCase> Cases { get; set; }
    public DbSet<LineOfDutyDocument> Documents { get; set; }
    public DbSet<LineOfDutyAppeal> Appeals { get; set; }
    public DbSet<LineOfDutyAuthority> Authorities { get; set; }
    public DbSet<MEDCONDetails> MEDCONDetails { get; set; }
    public DbSet<INCAPDetails> INCAPDetails { get; set; }
    public DbSet<TimelineStep> TimelineSteps { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // LineOfDutyCase configuration
        modelBuilder.Entity<LineOfDutyCase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CaseId).IsUnique();

            entity.HasMany(e => e.Documents)
                  .WithOne()
                  .HasForeignKey(d => d.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Appeals)
                  .WithOne()
                  .HasForeignKey(a => a.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Authorities)
                  .WithOne()
                  .HasForeignKey(a => a.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TimelineSteps)
                  .WithOne()
                  .HasForeignKey(t => t.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MEDCON)
                  .WithOne()
                  .HasForeignKey<LineOfDutyCase>(e => e.MEDCONId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.INCAP)
                  .WithOne()
                  .HasForeignKey<LineOfDutyCase>(e => e.INCAPId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // LineOfDutyAppeal — navigational relationship to appellate authority
        modelBuilder.Entity<LineOfDutyAppeal>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.AppellateAuthority)
                  .WithMany()
                  .HasForeignKey(e => e.AppellateAuthorityId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // TimelineStep — navigational relationship to responsible authority
        modelBuilder.Entity<TimelineStep>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ResponsibleAuthority)
                  .WithMany()
                  .HasForeignKey(e => e.ResponsibleAuthorityId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Simple entities
        modelBuilder.Entity<LineOfDutyDocument>().HasKey(e => e.Id);
        modelBuilder.Entity<LineOfDutyAuthority>().HasKey(e => e.Id);
        modelBuilder.Entity<MEDCONDetails>().HasKey(e => e.Id);
        modelBuilder.Entity<INCAPDetails>().HasKey(e => e.Id);
    }
}
