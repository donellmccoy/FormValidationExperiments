using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class CaseDialogueCommentConfiguration : IEntityTypeConfiguration<CaseDialogueComment>
{
    public void Configure(EntityTypeBuilder<CaseDialogueComment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Text).HasMaxLength(4000);
        builder.Property(e => e.AuthorName).HasMaxLength(256);
        builder.Property(e => e.AuthorRole).HasMaxLength(256);
        builder.Property(e => e.AcknowledgedBy).HasMaxLength(256);

        builder.HasIndex(e => new { e.LineOfDutyCaseId, e.CreatedDate })
               .IsDescending(false, true);

        builder.HasIndex(e => e.ParentCommentId);
    }
}
