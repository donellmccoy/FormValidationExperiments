using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameWorkflowStepHistoriesToWorkflowStateHistories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "WorkflowStepHistories",
                newName: "WorkflowStateHistories");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStepHistories_LineOfDutyCaseId_WorkflowState",
                table: "WorkflowStateHistories",
                newName: "IX_WorkflowStateHistories_LineOfDutyCaseId_WorkflowState");

            // Rename PK and FK constraints to match new table name
            migrationBuilder.Sql("EXEC sp_rename N'PK_WorkflowStepHistories', N'PK_WorkflowStateHistories', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_WorkflowStepHistories_Cases_LineOfDutyCaseId', N'FK_WorkflowStateHistories_Cases_LineOfDutyCaseId', N'OBJECT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "WorkflowStateHistories",
                newName: "WorkflowStepHistories");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStateHistories_LineOfDutyCaseId_WorkflowState",
                table: "WorkflowStepHistories",
                newName: "IX_WorkflowStepHistories_LineOfDutyCaseId_WorkflowState");

            migrationBuilder.Sql("EXEC sp_rename N'PK_WorkflowStateHistories', N'PK_WorkflowStepHistories', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_WorkflowStateHistories_Cases_LineOfDutyCaseId', N'FK_WorkflowStepHistories_Cases_LineOfDutyCaseId', N'OBJECT';");
        }
    }
}
