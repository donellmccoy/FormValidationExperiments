using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Data.Configurations;

public class MEDCONDetailsConfiguration : IEntityTypeConfiguration<MEDCONDetails>
{
    public void Configure(EntityTypeBuilder<MEDCONDetails> builder)
    {
        builder.HasKey(e => e.Id);
    }
}
