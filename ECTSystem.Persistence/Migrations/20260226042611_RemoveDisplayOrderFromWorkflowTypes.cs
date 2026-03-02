using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDisplayOrderFromWorkflowTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "WorkflowTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "WorkflowTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "DisplayOrder",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "DisplayOrder",
                value: 2);
        }
    }
}
