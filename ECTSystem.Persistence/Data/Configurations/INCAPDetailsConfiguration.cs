using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class INCAPDetailsConfiguration : IEntityTypeConfiguration<INCAPDetails>
{
    public void Configure(EntityTypeBuilder<INCAPDetails> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CivilianIncomeLoss).HasPrecision(18, 2);
    }
}
