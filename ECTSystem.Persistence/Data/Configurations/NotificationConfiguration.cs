using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.LineOfDutyCaseId);

        builder.Property(e => e.Title).HasMaxLength(256);
        builder.Property(e => e.Message).HasMaxLength(2000);
        builder.Property(e => e.Recipient).HasMaxLength(256);
        builder.Property(e => e.NotificationType).HasMaxLength(100);
    }
}
