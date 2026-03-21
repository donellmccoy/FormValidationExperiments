using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkflowStateHistoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "WorkflowStateHistories");

            migrationBuilder.DropColumn(
                name: "PerformedBy",
                table: "WorkflowStateHistories");

            migrationBuilder.DropColumn(
                name: "SignedBy",
                table: "WorkflowStateHistories");

            migrationBuilder.DropColumn(
                name: "SignedDate",
                table: "WorkflowStateHistories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "WorkflowStateHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PerformedBy",
                table: "WorkflowStateHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedBy",
                table: "WorkflowStateHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedDate",
                table: "WorkflowStateHistories",
                type: "datetime2",
                nullable: true);
        }
    }
}
