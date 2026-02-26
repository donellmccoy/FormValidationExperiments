using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameWorkflowStateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "LineOfDutyWorkflowState",
                newName: "WorkflowState");

            migrationBuilder.RenameIndex(
                name: "IX_LineOfDutyWorkflowState_Name",
                table: "WorkflowState",
                newName: "IX_WorkflowState_Name");

            migrationBuilder.Sql(
                "EXEC sp_rename 'PK_LineOfDutyWorkflowState', 'PK_WorkflowState'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "WorkflowState",
                newName: "LineOfDutyWorkflowState");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowState_Name",
                table: "LineOfDutyWorkflowState",
                newName: "IX_LineOfDutyWorkflowState_Name");

            migrationBuilder.Sql(
                "EXEC sp_rename 'PK_WorkflowState', 'PK_LineOfDutyWorkflowState'");
        }
    }
}
