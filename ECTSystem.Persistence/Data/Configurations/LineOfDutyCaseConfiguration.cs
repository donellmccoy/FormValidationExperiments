using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data.Configurations;

public class LineOfDutyCaseConfiguration : IEntityTypeConfiguration<LineOfDutyCase>
{
    public void Configure(EntityTypeBuilder<LineOfDutyCase> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.CaseId).IsUnique();

        // Indexes for common query patterns
        builder.HasIndex(e => e.MemberId);
        builder.HasIndex(e => e.CreatedDate);
        builder.HasIndex(e => new { e.MemberId, e.CreatedDate })
               .HasDatabaseName("IX_Cases_MemberId_CreatedDate");

        builder.HasMany(e => e.WitnessStatements)
               .WithOne()
               .HasForeignKey(w => w.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.AuditComments)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Documents)
               .WithOne()
               .HasForeignKey(d => d.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Appeals)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Authorities)
               .WithOne()
               .HasForeignKey(a => a.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(e => e.Notifications)
               .WithOne()
               .HasForeignKey(n => n.LineOfDutyCaseId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasOne(e => e.MEDCON)
               .WithOne()
               .HasForeignKey<LineOfDutyCase>(e => e.MEDCONId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.INCAP)
               .WithOne()
               .HasForeignKey<LineOfDutyCase>(e => e.INCAPId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Member)
               .WithMany()
               .HasForeignKey(e => e.MemberId)
               .OnDelete(DeleteBehavior.NoAction);

        // ── String length constraints ──

        // Basic case info
        builder.Property(e => e.MemberName).HasMaxLength(150);
        builder.Property(e => e.MemberRank).HasMaxLength(50);
        builder.Property(e => e.ServiceNumber).HasMaxLength(50);
        builder.Property(e => e.Unit).HasMaxLength(200);
        builder.Property(e => e.FromLine).HasMaxLength(200);
        builder.Property(e => e.IncidentDescription).HasMaxLength(4000);

        // Part I – Orders / Duty Period (HHmm times)
        builder.Property(e => e.MemberOrdersStartTime).HasMaxLength(10);
        builder.Property(e => e.MemberOrdersEndTime).HasMaxLength(10);

        // Medical Assessment
        builder.Property(e => e.TreatmentFacilityName).HasMaxLength(256);
        builder.Property(e => e.ClinicalDiagnosis).HasMaxLength(4000);
        builder.Property(e => e.MedicalFindings).HasMaxLength(4000);
        builder.Property(e => e.PsychiatricEvalResults).HasMaxLength(4000);
        builder.Property(e => e.OtherRelevantConditions).HasMaxLength(4000);
        builder.Property(e => e.OtherTestResults).HasMaxLength(2000);
        builder.Property(e => e.MedicalRecommendation).HasMaxLength(4000);

        // Commander Review
        builder.Property(e => e.OtherSourcesDescription).HasMaxLength(2000);
        builder.Property(e => e.MisconductExplanation).HasMaxLength(4000);
        builder.Property(e => e.CommanderToLine).HasMaxLength(200);
        builder.Property(e => e.CommanderFromLine).HasMaxLength(200);

        // AWOL dates/times
        builder.Property(e => e.AbsentWithoutLeaveDate1).HasMaxLength(50);
        builder.Property(e => e.AbsentWithoutLeaveTime1).HasMaxLength(10);
        builder.Property(e => e.AbsentWithoutLeaveDate2).HasMaxLength(50);
        builder.Property(e => e.AbsentWithoutLeaveTime2).HasMaxLength(10);

        // Witness name/address
        builder.Property(e => e.WitnessNameAddress1).HasMaxLength(500);
        builder.Property(e => e.WitnessNameAddress2).HasMaxLength(500);
        builder.Property(e => e.WitnessNameAddress3).HasMaxLength(500);
        builder.Property(e => e.WitnessNameAddress4).HasMaxLength(500);
        builder.Property(e => e.WitnessNameAddress5).HasMaxLength(500);

        // Findings
        builder.Property(e => e.ProximateCause).HasMaxLength(4000);
        builder.Property(e => e.PSCDocumentation).HasMaxLength(4000);
        builder.Property(e => e.ToxicologyReport).HasMaxLength(4000);

        // Part II – Provider signature block
        builder.Property(e => e.ProviderNameRank).HasMaxLength(150);
        builder.Property(e => e.ProviderDate).HasMaxLength(50);
        builder.Property(e => e.ProviderSignature).HasMaxLength(2000);

        // Part III – Commander signature block
        builder.Property(e => e.CommanderNameRank).HasMaxLength(150);
        builder.Property(e => e.CommanderDate).HasMaxLength(50);
        builder.Property(e => e.CommanderSignature).HasMaxLength(2000);

        // Part IV – SJA/Legal
        builder.Property(e => e.SjaNameRank).HasMaxLength(150);
        builder.Property(e => e.SjaDate).HasMaxLength(50);

        // Part V – Wing CC / Appointing Authority
        builder.Property(e => e.WingCcSignature).HasMaxLength(2000);
        builder.Property(e => e.AppointingAuthorityNameRank).HasMaxLength(150);
        builder.Property(e => e.AppointingAuthorityDate).HasMaxLength(50);
        builder.Property(e => e.AppointingAuthoritySignature).HasMaxLength(2000);

        // Part VI – Formal Board Review
        builder.Property(e => e.MedicalReviewText).HasMaxLength(4000);
        builder.Property(e => e.MedicalReviewerNameRank).HasMaxLength(150);
        builder.Property(e => e.MedicalReviewDate).HasMaxLength(50);
        builder.Property(e => e.MedicalReviewerSignature).HasMaxLength(2000);
        builder.Property(e => e.LegalReviewText).HasMaxLength(4000);
        builder.Property(e => e.LegalReviewerNameRank).HasMaxLength(150);
        builder.Property(e => e.LegalReviewDate).HasMaxLength(50);
        builder.Property(e => e.LegalReviewerSignature).HasMaxLength(2000);
        builder.Property(e => e.LodBoardChairNameRank).HasMaxLength(150);
        builder.Property(e => e.LodBoardChairDate).HasMaxLength(50);
        builder.Property(e => e.LodBoardChairSignature).HasMaxLength(2000);

        // Part VII – Approving Authority
        builder.Property(e => e.ApprovingAuthorityNameRank).HasMaxLength(150);
        builder.Property(e => e.ApprovingAuthorityDate).HasMaxLength(50);
        builder.Property(e => e.ApprovingAuthoritySignature).HasMaxLength(2000);

        // Special handling
        builder.Property(e => e.SARCCoordination).HasMaxLength(500);

        // Checkout
        builder.Property(e => e.CheckedOutBy).HasMaxLength(256);
        builder.Property(e => e.CheckedOutByName).HasMaxLength(150);

        // Contact
        builder.Property(e => e.PointOfContact).HasMaxLength(256);
    }
}
