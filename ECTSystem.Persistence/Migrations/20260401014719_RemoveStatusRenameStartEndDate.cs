using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStatusRenameStartEndDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "WorkflowStateHistories");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "WorkflowStateHistories",
                newName: "EnteredDate");

            migrationBuilder.RenameColumn(
                name: "EndDate",
                table: "WorkflowStateHistories",
                newName: "ExitDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExitDate",
                table: "WorkflowStateHistories",
                newName: "EndDate");

            migrationBuilder.RenameColumn(
                name: "EnteredDate",
                table: "WorkflowStateHistories",
                newName: "StartDate");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "WorkflowStateHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
