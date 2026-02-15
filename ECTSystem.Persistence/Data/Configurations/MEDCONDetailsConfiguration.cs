using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class MEDCONDetailsConfiguration : IEntityTypeConfiguration<MEDCONDetail>
{
    public void Configure(EntityTypeBuilder<MEDCONDetail> builder)
    {
        builder.HasKey(e => e.Id);
    }
}
