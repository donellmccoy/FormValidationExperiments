using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class LineOfDutyAuthorityConfiguration : IEntityTypeConfiguration<LineOfDutyAuthority>
{
    public void Configure(EntityTypeBuilder<LineOfDutyAuthority> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Comments)
               .HasConversion(StringListConversion.Converter)
               .Metadata.SetValueComparer(StringListConversion.Comparer);
    }
}
