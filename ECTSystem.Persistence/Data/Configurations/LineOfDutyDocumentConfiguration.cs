using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class LineOfDutyDocumentConfiguration : IEntityTypeConfiguration<LineOfDutyDocument>
{
    public void Configure(EntityTypeBuilder<LineOfDutyDocument> builder)
    {
        builder.HasKey(e => e.Id);

        // Composite index optimised for the documents grid query, which filters by
        // LineOfDutyCaseId and orders by UploadDate desc, Id desc. Supports both the
        // paged SELECT and the COUNT_BIG(*) issued when $count=true.
        builder.HasIndex(e => new { e.LineOfDutyCaseId, e.UploadDate, e.Id })
            .IsDescending(false, true, true);

        builder.Property(e => e.BlobPath).HasMaxLength(1024);
        builder.Property(e => e.ContentType).HasMaxLength(256);
        builder.Property(e => e.FileName).HasMaxLength(512);

        builder.Property(e => e.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(1000);
    }
}
