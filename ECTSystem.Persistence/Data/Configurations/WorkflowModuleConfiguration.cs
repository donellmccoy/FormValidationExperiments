using ECTSystem.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECTSystem.Persistence.Data.Configurations;

public class WorkflowModuleConfiguration : IEntityTypeConfiguration<WorkflowModule>
{
    public void Configure(EntityTypeBuilder<WorkflowModule> builder)
    {
        builder.ToTable("WorkflowModules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(e => e.ModifiedDate).HasDefaultValueSql("GETUTCDATE()");

        builder.HasData(
            new WorkflowModule { Id = 1, Name = "AFRC", Description = "Air Force Reserve Command workflow module." },
            new WorkflowModule { Id = 2, Name = "ANG",  Description = "Air National Guard workflow module."        }
        );
    }
}
