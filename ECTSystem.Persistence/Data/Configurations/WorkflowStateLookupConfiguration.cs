using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECTSystem.Persistence.Data.Configurations;

public class WorkflowStateLookupConfiguration : IEntityTypeConfiguration<WorkflowStateLookup>
{
    public void Configure(EntityTypeBuilder<WorkflowStateLookup> builder)
    {
        builder.ToTable("WorkflowStates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.HasIndex(e => new { e.Name, e.WorkflowTypeId }).IsUnique();
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(e => e.ModifiedDate).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(e => e.WorkflowType)
            .WithMany(t => t.WorkflowStates)
            .HasForeignKey(e => e.WorkflowTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Informal workflow states (WorkflowTypeId = 1, IDs 1–12)
        builder.HasData(
            new WorkflowStateLookup { Id = (int)WorkflowState.MemberInformationEntry,    Name = "Member Information Entry",    Description = "Enter member identification and incident details to initiate the LOD case.",                         DisplayOrder = 1,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.MedicalTechnicianReview,   Name = "Medical Technician Review",   Description = "Medical technician reviews the injury/illness and documents clinical findings.",                       DisplayOrder = 2,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.MedicalOfficerReview,      Name = "Medical Officer Review",      Description = "Medical officer reviews the technician's findings and provides a clinical assessment.",                 DisplayOrder = 3,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.UnitCommanderReview,       Name = "Unit CC Review",              Description = "Unit commander reviews the case and submits a recommendation for the LOD determination.",              DisplayOrder = 4,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.WingJudgeAdvocateReview,   Name = "Wing JA Review",              Description = "Wing Judge Advocate reviews the case for legal sufficiency and compliance.",                           DisplayOrder = 5,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.AppointingAuthorityReview, Name = "Appointing Authority Review", Description = "Appointing authority reviews the case and issues a formal LOD determination.",                        DisplayOrder = 6,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.WingCommanderReview,       Name = "Wing CC Review",              Description = "Wing commander reviews the case and renders a preliminary LOD determination.",                         DisplayOrder = 7,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.BoardMedicalTechnicianReview,     Name = "Board Technician Review",     Description = "Board medical technician reviews the case file for completeness and accuracy.",                       DisplayOrder = 8,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.BoardMedicalOfficerReview,        Name = "Board Medical Review",        Description = "Board medical officer reviews all medical evidence and provides a formal assessment.",                 DisplayOrder = 9,  WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.BoardLegalReview,          Name = "Board Legal Review",          Description = "Board legal counsel reviews the case for legal sufficiency before final decision.",                    DisplayOrder = 10, WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.BoardAdministratorReview,          Name = "Board Admin Review",          Description = "Board administrative officer finalizes the case package and prepares the formal determination.",      DisplayOrder = 11, WorkflowTypeId = 1 },
            new WorkflowStateLookup { Id = (int)WorkflowState.Completed,                 Name = "Completed",                   Description = "LOD determination has been finalized and the case is closed.",                                        DisplayOrder = 12, WorkflowTypeId = 1 },

            // Formal workflow states (WorkflowTypeId = 2, IDs 13–24)
            new WorkflowStateLookup { Id = 13, Name = "Member Information Entry",    Description = "Enter member identification and incident details to initiate the LOD case.",                         DisplayOrder = 1,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 14, Name = "Medical Technician Review",   Description = "Medical technician reviews the injury/illness and documents clinical findings.",                       DisplayOrder = 2,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 15, Name = "Medical Officer Review",      Description = "Medical officer reviews the technician's findings and provides a clinical assessment.",                 DisplayOrder = 3,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 16, Name = "Unit CC Review",              Description = "Unit commander reviews the case and submits a recommendation for the LOD determination.",              DisplayOrder = 4,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 17, Name = "Wing JA Review",              Description = "Wing Judge Advocate reviews the case for legal sufficiency and compliance.",                           DisplayOrder = 5,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 18, Name = "Appointing Authority Review", Description = "Appointing authority reviews the case and issues a formal LOD determination.",                        DisplayOrder = 6,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 19, Name = "Wing CC Review",              Description = "Wing commander reviews the case and renders a preliminary LOD determination.",                         DisplayOrder = 7,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 20, Name = "Board Technician Review",     Description = "Board medical technician reviews the case file for completeness and accuracy.",                       DisplayOrder = 8,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 21, Name = "Board Medical Review",        Description = "Board medical officer reviews all medical evidence and provides a formal assessment.",                 DisplayOrder = 9,  WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 22, Name = "Board Legal Review",          Description = "Board legal counsel reviews the case for legal sufficiency before final decision.",                    DisplayOrder = 10, WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 23, Name = "Board Admin Review",          Description = "Board administrative officer finalizes the case package and prepares the formal determination.",      DisplayOrder = 11, WorkflowTypeId = 2 },
            new WorkflowStateLookup { Id = 24, Name = "Completed",                   Description = "LOD determination has been finalized and the case is closed.",                                        DisplayOrder = 12, WorkflowTypeId = 2 }
        );
    }
}
