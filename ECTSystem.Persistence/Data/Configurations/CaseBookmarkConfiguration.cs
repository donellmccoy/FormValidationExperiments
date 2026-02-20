using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class CaseBookmarkConfiguration : IEntityTypeConfiguration<CaseBookmark>
{
    public void Configure(EntityTypeBuilder<CaseBookmark> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.UserId, e.LineOfDutyCaseId }).IsUnique();

        builder.Property(e => e.UserId).HasMaxLength(256);

        builder.HasOne<LineOfDutyCase>()
               .WithMany()
               .HasForeignKey(e => e.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
