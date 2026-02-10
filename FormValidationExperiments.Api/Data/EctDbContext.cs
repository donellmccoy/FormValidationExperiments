using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.Api.Data;

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
    public DbSet<LineOfDutyDocument> Documents { get; set; }
    public DbSet<LineOfDutyAppeal> Appeals { get; set; }
    public DbSet<LineOfDutyAuthority> Authorities { get; set; }
    public DbSet<MEDCONDetails> MEDCONDetails { get; set; }
    public DbSet<INCAPDetails> INCAPDetails { get; set; }
    public DbSet<TimelineStep> TimelineSteps { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // JSON value converter for List<string> properties
        var stringListConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 ?? new()).SequenceEqual(c2 ?? new()),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        // LineOfDutyCase configuration
        modelBuilder.Entity<LineOfDutyCase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CaseId).IsUnique();

            // JSON columns for List<string> properties
            entity.Property(e => e.WitnessStatements)
                  .HasConversion(stringListConverter)
                  .Metadata.SetValueComparer(stringListComparer);

            entity.Property(e => e.AuditComments)
                  .HasConversion(stringListConverter)
                  .Metadata.SetValueComparer(stringListComparer);

            entity.HasMany(e => e.Documents)
                  .WithOne()
                  .HasForeignKey(d => d.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.Appeals)
                  .WithOne()
                  .HasForeignKey(a => a.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.Authorities)
                  .WithOne()
                  .HasForeignKey(a => a.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.TimelineSteps)
                  .WithOne()
                  .HasForeignKey(t => t.LineOfDutyCaseId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.MEDCON)
                  .WithOne()
                  .HasForeignKey<LineOfDutyCase>(e => e.MEDCONId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.INCAP)
                  .WithOne()
                  .HasForeignKey<LineOfDutyCase>(e => e.INCAPId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // LineOfDutyAppeal — navigational relationship to appellate authority
        modelBuilder.Entity<LineOfDutyAppeal>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.NewEvidence)
                  .HasConversion(stringListConverter)
                  .Metadata.SetValueComparer(stringListComparer);

            entity.HasOne(e => e.AppellateAuthority)
                  .WithMany()
                  .HasForeignKey(e => e.AppellateAuthorityId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // LineOfDutyAuthority — JSON conversion for Comments
        modelBuilder.Entity<LineOfDutyAuthority>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Comments)
                  .HasConversion(stringListConverter)
                  .Metadata.SetValueComparer(stringListComparer);
        });

        // TimelineStep — navigational relationship to responsible authority
        modelBuilder.Entity<TimelineStep>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ResponsibleAuthority)
                  .WithMany()
                  .HasForeignKey(e => e.ResponsibleAuthorityId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // Simple entities
        modelBuilder.Entity<LineOfDutyDocument>().HasKey(e => e.Id);
        modelBuilder.Entity<MEDCONDetails>().HasKey(e => e.Id);
        modelBuilder.Entity<INCAPDetails>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CivilianIncomeLoss).HasPrecision(18, 2);
        });
    }
}
