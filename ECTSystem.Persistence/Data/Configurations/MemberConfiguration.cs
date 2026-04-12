using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FirstName).HasMaxLength(100);
        builder.Property(e => e.LastName).HasMaxLength(100);
        builder.Property(e => e.MiddleInitial).HasMaxLength(10);
        builder.Property(e => e.Rank).HasMaxLength(50);
        builder.Property(e => e.ServiceNumber).HasMaxLength(50);
        builder.Property(e => e.Unit).HasMaxLength(200);
    }
}
