using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Data.Configurations;

public class TimelineStepConfiguration : IEntityTypeConfiguration<TimelineStep>
{
    public void Configure(EntityTypeBuilder<TimelineStep> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.ResponsibleAuthority)
               .WithMany()
               .HasForeignKey(e => e.ResponsibleAuthorityId)
               .OnDelete(DeleteBehavior.NoAction);
    }
}
