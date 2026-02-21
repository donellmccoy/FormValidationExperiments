using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECTSystem.Persistence.Data.Configurations;

public class LineOfDutyWorkflowStateLookupConfiguration : IEntityTypeConfiguration<LineOfDutyWorkflowStateLookup>
{
    public void Configure(EntityTypeBuilder<LineOfDutyWorkflowStateLookup> builder)
    {
        builder.ToTable("LineOfDutyWorkflowState");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.MemberInformationEntry,    Name = "Member Information Entry",   Description = "Enter member identification and incident details to initiate the LOD case.",                              DisplayOrder = 1  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.MedicalTechnicianReview,   Name = "Medical Technician Review",  Description = "Medical technician reviews the injury/illness and documents clinical findings.",                        DisplayOrder = 2  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.MedicalOfficerReview,      Name = "Medical Officer Review",     Description = "Medical officer reviews the technician's findings and provides a clinical assessment.",                  DisplayOrder = 3  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.UnitCommanderReview,       Name = "Unit CC Review",             Description = "Unit commander reviews the case and submits a recommendation for the LOD determination.",               DisplayOrder = 4  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.WingJudgeAdvocateReview,   Name = "Wing JA Review",             Description = "Wing Judge Advocate reviews the case for legal sufficiency and compliance.",                            DisplayOrder = 5  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.AppointingAuthorityReview, Name = "Appointing Authority Review", Description = "Appointing authority reviews the case and issues a formal LOD determination.",                        DisplayOrder = 6  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.WingCommanderReview,       Name = "Wing CC Review",             Description = "Wing commander reviews the case and renders a preliminary LOD determination.",                          DisplayOrder = 7  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.BoardTechnicianReview,     Name = "Board Technician Review",    Description = "Board medical technician reviews the case file for completeness and accuracy.",                        DisplayOrder = 8  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.BoardMedicalReview,        Name = "Board Medical Review",       Description = "Board medical officer reviews all medical evidence and provides a formal assessment.",                  DisplayOrder = 9  },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.BoardLegalReview,          Name = "Board Legal Review",         Description = "Board legal counsel reviews the case for legal sufficiency before final decision.",                     DisplayOrder = 10 },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.BoardAdminReview,          Name = "Board Admin Review",         Description = "Board administrative officer finalizes the case package and prepares the formal determination.",       DisplayOrder = 11 },
            new LineOfDutyWorkflowStateLookup { Id = (int)LineOfDutyWorkflowState.Completed,                 Name = "Completed",                  Description = "LOD determination has been finalized and the case is closed.",                                         DisplayOrder = 12 }
        );
    }
}
