using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class LineOfDutyAppealConfiguration : IEntityTypeConfiguration<LineOfDutyAppeal>
{
    public void Configure(EntityTypeBuilder<LineOfDutyAppeal> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.NewEvidence)
               .HasConversion(StringListConversion.Converter)
               .Metadata.SetValueComparer(StringListConversion.Comparer);

        builder.HasOne(e => e.AppellateAuthority)
               .WithMany()
               .HasForeignKey(e => e.AppellateAuthorityId)
               .OnDelete(DeleteBehavior.NoAction);
    }
}
