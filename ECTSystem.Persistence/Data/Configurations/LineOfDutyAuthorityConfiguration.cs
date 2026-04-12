using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class LineOfDutyAuthorityConfiguration : IEntityTypeConfiguration<LineOfDutyAuthority>
{
    public void Configure(EntityTypeBuilder<LineOfDutyAuthority> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.LineOfDutyCaseId);

        builder.Property(e => e.Comments)
               .HasConversion(StringListConversion.Converter)
               .Metadata.SetValueComparer(StringListConversion.Comparer);

        builder.Property(e => e.Name).HasMaxLength(150);
        builder.Property(e => e.Rank).HasMaxLength(50);
        builder.Property(e => e.Title).HasMaxLength(200);
        builder.Property(e => e.Role).HasMaxLength(100);
        builder.Property(e => e.Recommendation).HasMaxLength(2000);
    }
}
