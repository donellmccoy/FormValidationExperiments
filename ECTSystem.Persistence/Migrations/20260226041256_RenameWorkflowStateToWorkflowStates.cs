using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameWorkflowStateToWorkflowStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowState",
                table: "WorkflowState");

            migrationBuilder.RenameTable(
                name: "WorkflowState",
                newName: "WorkflowStates");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowState_Name",
                table: "WorkflowStates",
                newName: "IX_WorkflowStates_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowStates",
                table: "WorkflowStates",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowStates",
                table: "WorkflowStates");

            migrationBuilder.RenameTable(
                name: "WorkflowStates",
                newName: "WorkflowState");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStates_Name",
                table: "WorkflowState",
                newName: "IX_WorkflowState_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowState",
                table: "WorkflowState",
                column: "Id");
        }
    }
}
