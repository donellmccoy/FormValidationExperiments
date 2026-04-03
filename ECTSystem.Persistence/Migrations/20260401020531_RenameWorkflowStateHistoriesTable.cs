using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameWorkflowStateHistoriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStateHistories_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowStateHistories",
                table: "WorkflowStateHistories");

            migrationBuilder.RenameTable(
                name: "WorkflowStateHistories",
                newName: "WorkflowStateHistory");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStateHistories_LineOfDutyCaseId_WorkflowState",
                table: "WorkflowStateHistory",
                newName: "IX_WorkflowStateHistory_LineOfDutyCaseId_WorkflowState");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStateHistories_LineOfDutyCaseId_CreatedDate_Id",
                table: "WorkflowStateHistory",
                newName: "IX_WorkflowStateHistory_LineOfDutyCaseId_CreatedDate_Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowStateHistory",
                table: "WorkflowStateHistory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStateHistory_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistory",
                column: "LineOfDutyCaseId",
                principalTable: "Cases",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStateHistory_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowStateHistory",
                table: "WorkflowStateHistory");

            migrationBuilder.RenameTable(
                name: "WorkflowStateHistory",
                newName: "WorkflowStateHistories");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStateHistory_LineOfDutyCaseId_WorkflowState",
                table: "WorkflowStateHistories",
                newName: "IX_WorkflowStateHistories_LineOfDutyCaseId_WorkflowState");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStateHistory_LineOfDutyCaseId_CreatedDate_Id",
                table: "WorkflowStateHistories",
                newName: "IX_WorkflowStateHistories_LineOfDutyCaseId_CreatedDate_Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowStateHistories",
                table: "WorkflowStateHistories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStateHistories_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistories",
                column: "LineOfDutyCaseId",
                principalTable: "Cases",
                principalColumn: "Id");
        }
    }
}
