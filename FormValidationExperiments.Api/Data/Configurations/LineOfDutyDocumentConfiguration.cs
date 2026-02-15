using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.Api.Data.Configurations;

public class LineOfDutyDocumentConfiguration : IEntityTypeConfiguration<LineOfDutyDocument>
{
    public void Configure(EntityTypeBuilder<LineOfDutyDocument> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Content).HasColumnType("varbinary(max)");
        builder.Property(e => e.ContentType).HasMaxLength(256);
        builder.Property(e => e.FileName).HasMaxLength(512);
    }
}
