using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class AuditCommentConfiguration : IEntityTypeConfiguration<AuditComment>
{
    public void Configure(EntityTypeBuilder<AuditComment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.LineOfDutyCaseId);

        builder.Property(e => e.Text).HasMaxLength(4000);
    }
}
