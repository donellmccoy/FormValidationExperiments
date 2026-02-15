using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.Api.Data.Configurations;

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
               .WithOne()
               .HasForeignKey(d => d.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Appeals)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Authorities)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.TimelineSteps)
               .WithOne()
               .HasForeignKey(t => t.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.NoAction);

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
