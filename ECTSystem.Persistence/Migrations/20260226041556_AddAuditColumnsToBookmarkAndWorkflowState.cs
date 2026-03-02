using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumnsToBookmarkAndWorkflowState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowStates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "WorkflowStates",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "WorkflowStates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "WorkflowStates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CaseBookmarks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "CaseBookmarks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CaseBookmarks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "CaseBookmarks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowStates");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "WorkflowStates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "WorkflowStates");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "WorkflowStates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CaseBookmarks");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "CaseBookmarks");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CaseBookmarks");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "CaseBookmarks");
        }
    }
}
