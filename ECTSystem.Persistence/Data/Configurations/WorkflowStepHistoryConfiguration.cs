using ECTSystem.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECTSystem.Persistence.Data.Configurations;

public class WorkflowStepHistoryConfiguration : IEntityTypeConfiguration<WorkflowStepHistory>
{
    public void Configure(EntityTypeBuilder<WorkflowStepHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.HasIndex(h => new { h.LineOfDutyCaseId, h.WorkflowState });

        builder.HasOne(h => h.LineOfDutyCase)
               .WithMany(c => c.WorkflowStepHistories)
               .HasForeignKey(h => h.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
