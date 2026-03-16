using ECTSystem.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECTSystem.Persistence.Data.Configurations;

public class WorkflowStateHistoryConfiguration : IEntityTypeConfiguration<WorkflowStateHistory>
{
    public void Configure(EntityTypeBuilder<WorkflowStateHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.HasIndex(h => new { h.LineOfDutyCaseId, h.WorkflowState });

        // Supports CurrentWorkflowState derivation: most-recent history entry by CreatedDate, Id
        builder.HasIndex(h => new { h.LineOfDutyCaseId, h.CreatedDate, h.Id })
               .IsDescending(false, true, true);

        builder.HasOne<LineOfDutyCase>()
               .WithMany(c => c.WorkflowStateHistories)
               .HasForeignKey(h => h.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
