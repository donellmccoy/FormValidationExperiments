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

        builder.Property(e => e.WitnessStatements)
               .HasConversion(StringListConversion.Converter)
               .Metadata.SetValueComparer(StringListConversion.Comparer);

        builder.Property(e => e.AuditComments)
               .HasConversion(StringListConversion.Converter)
               .Metadata.SetValueComparer(StringListConversion.Comparer);

        builder.HasMany(e => e.Documents)
               .WithOne(d => d.LineOfDutyCase)
               .HasForeignKey(d => d.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Appeals)
               .WithOne(a => a.LineOfDutyCase)
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Authorities)
               .WithOne(a => a.LineOfDutyCase)
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.TimelineSteps)
               .WithOne(t => t.LineOfDutyCase)
               .HasForeignKey(t => t.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Notifications)
               .WithOne(n => n.LineOfDutyCase)
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
               .WithMany(m => m.LineOfDutyCases)
               .HasForeignKey(e => e.MemberId)
               .OnDelete(DeleteBehavior.NoAction);
    }
}
