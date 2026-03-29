using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class WitnessStatementConfiguration : IEntityTypeConfiguration<WitnessStatement>
{
    public void Configure(EntityTypeBuilder<WitnessStatement> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.LineOfDutyCaseId);
    }
}
