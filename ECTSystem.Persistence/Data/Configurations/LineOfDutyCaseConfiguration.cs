using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class LineOfDutyCaseConfiguration : IEntityTypeConfiguration<LineOfDutyCase>
{
    public void Configure(EntityTypeBuilder<LineOfDutyCase> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.CaseId).IsUnique();

        // Indexes for common query patterns
        builder.HasIndex(e => e.MemberId);
        builder.HasIndex(e => e.CreatedDate);
        builder.HasIndex(e => new { e.MemberId, e.CreatedDate })
               .HasDatabaseName("IX_Cases_MemberId_CreatedDate");

        // CurrentWorkflowState is a computed CLR property — not a database column
        builder.Ignore(e => e.CurrentWorkflowState);

        builder.HasMany(e => e.WitnessStatements)
               .WithOne()
               .HasForeignKey(w => w.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.AuditComments)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Documents)
               .WithOne()
               .HasForeignKey(d => d.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Appeals)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Authorities)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Notifications)
               .WithOne()
               .HasForeignKey(n => n.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasOne(e => e.MEDCON)
               .WithOne()
               .HasForeignKey<LineOfDutyCase>(e => e.MEDCONId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.INCAP)
               .WithOne()
               .HasForeignKey<LineOfDutyCase>(e => e.INCAPId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Member)
               .WithMany()
               .HasForeignKey(e => e.MemberId)
               .OnDelete(DeleteBehavior.NoAction);
    }
}
