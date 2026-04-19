using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("Bookmarks");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.UserId, e.LineOfDutyCaseId }).IsUnique();

        builder.Property(e => e.UserId).HasMaxLength(256);

        builder.HasOne<LineOfDutyCase>()
               .WithMany(e => e.Bookmarks)
               .HasForeignKey(e => e.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
