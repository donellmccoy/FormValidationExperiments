using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECTSystem.Persistence.Data.Configurations;

public class WorkflowTypeConfiguration : IEntityTypeConfiguration<WorkflowType>
{
    public void Configure(EntityTypeBuilder<WorkflowType> builder)
    {
        builder.ToTable("WorkflowTypes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.HasIndex(e => new { e.Name, e.WorkflowModuleId }).IsUnique();
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(e => e.ModifiedDate).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(e => e.WorkflowModule)
            .WithMany(m => m.WorkflowTypes)
            .HasForeignKey(e => e.WorkflowModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new WorkflowType { Id = (int)LineOfDutyProcessType.Informal + 1, Name = "Informal", Description = "Informal LOD determination process.", WorkflowModuleId = 1 },
            new WorkflowType { Id = (int)LineOfDutyProcessType.Formal + 1,   Name = "Formal",   Description = "Formal LOD determination process.",   WorkflowModuleId = 1 }
        );
    }
}
