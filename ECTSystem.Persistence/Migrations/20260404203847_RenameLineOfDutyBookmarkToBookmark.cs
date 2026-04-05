using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameLineOfDutyBookmarkToBookmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "LineOfDutyBookmarks",
                newName: "Bookmarks");

            migrationBuilder.RenameIndex(
                name: "PK_LineOfDutyBookmarks",
                table: "Bookmarks",
                newName: "PK_Bookmarks");

            migrationBuilder.RenameIndex(
                name: "IX_LineOfDutyBookmarks_LineOfDutyCaseId",
                table: "Bookmarks",
                newName: "IX_Bookmarks_LineOfDutyCaseId");

            migrationBuilder.RenameIndex(
                name: "IX_LineOfDutyBookmarks_UserId_LineOfDutyCaseId",
                table: "Bookmarks",
                newName: "IX_Bookmarks_UserId_LineOfDutyCaseId");

            migrationBuilder.Sql(
                "EXEC sp_rename N'FK_LineOfDutyBookmarks_Cases_LineOfDutyCaseId', N'FK_Bookmarks_Cases_LineOfDutyCaseId', N'OBJECT'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EnteredDate",
                table: "WorkflowStateHistory",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "EnteredDate",
                table: "WorkflowStateHistory",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.Sql(
                "EXEC sp_rename N'FK_Bookmarks_Cases_LineOfDutyCaseId', N'FK_LineOfDutyBookmarks_Cases_LineOfDutyCaseId', N'OBJECT'");

            migrationBuilder.RenameIndex(
                name: "IX_Bookmarks_UserId_LineOfDutyCaseId",
                table: "Bookmarks",
                newName: "IX_LineOfDutyBookmarks_UserId_LineOfDutyCaseId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookmarks_LineOfDutyCaseId",
                table: "Bookmarks",
                newName: "IX_LineOfDutyBookmarks_LineOfDutyCaseId");

            migrationBuilder.RenameIndex(
                name: "PK_Bookmarks",
                table: "Bookmarks",
                newName: "PK_LineOfDutyBookmarks");

            migrationBuilder.RenameTable(
                name: "Bookmarks",
                newName: "LineOfDutyBookmarks");
        }
    }
}
