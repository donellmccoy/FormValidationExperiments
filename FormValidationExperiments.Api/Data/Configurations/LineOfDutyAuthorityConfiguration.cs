using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.Api.Data.Configurations;

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
