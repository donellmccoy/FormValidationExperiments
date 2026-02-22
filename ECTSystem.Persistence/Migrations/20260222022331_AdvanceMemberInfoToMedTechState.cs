using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdvanceMemberInfoToMedTechState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Advance all cases currently at MemberInformationEntry (1) to MedicalTechnicianReview (2).
            // Start LOD now auto-advances past the MemberInformationEntry state.
            migrationBuilder.Sql(
                "UPDATE Cases SET WorkflowState = 2 WHERE WorkflowState = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Cases SET WorkflowState = 1 WHERE WorkflowState = 2");
        }
    }
}
