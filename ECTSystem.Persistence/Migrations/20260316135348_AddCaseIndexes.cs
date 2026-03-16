using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkflowState",
                table: "Cases");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStateHistories_LineOfDutyCaseId_CreatedDate_Id",
                table: "WorkflowStateHistories",
                columns: new[] { "LineOfDutyCaseId", "CreatedDate", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CreatedDate",
                table: "Cases",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_MemberId_CreatedDate",
                table: "Cases",
                columns: new[] { "MemberId", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStateHistories_LineOfDutyCaseId_CreatedDate_Id",
                table: "WorkflowStateHistories");

            migrationBuilder.DropIndex(
                name: "IX_Cases_CreatedDate",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_MemberId_CreatedDate",
                table: "Cases");

            migrationBuilder.AddColumn<int>(
                name: "WorkflowState",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
