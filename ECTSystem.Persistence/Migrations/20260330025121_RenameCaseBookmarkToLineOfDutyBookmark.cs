using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCaseBookmarkToLineOfDutyBookmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "CaseBookmarks",
                newName: "LineOfDutyBookmarks");

            migrationBuilder.RenameIndex(
                name: "PK_CaseBookmarks",
                table: "LineOfDutyBookmarks",
                newName: "PK_LineOfDutyBookmarks");

            migrationBuilder.RenameIndex(
                name: "IX_CaseBookmarks_LineOfDutyCaseId",
                table: "LineOfDutyBookmarks",
                newName: "IX_LineOfDutyBookmarks_LineOfDutyCaseId");

            migrationBuilder.RenameIndex(
                name: "IX_CaseBookmarks_UserId_LineOfDutyCaseId",
                table: "LineOfDutyBookmarks",
                newName: "IX_LineOfDutyBookmarks_UserId_LineOfDutyCaseId");

            migrationBuilder.Sql(
                "EXEC sp_rename N'FK_CaseBookmarks_Cases_LineOfDutyCaseId', N'FK_LineOfDutyBookmarks_Cases_LineOfDutyCaseId', N'OBJECT'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "EXEC sp_rename N'FK_LineOfDutyBookmarks_Cases_LineOfDutyCaseId', N'FK_CaseBookmarks_Cases_LineOfDutyCaseId', N'OBJECT'");

            migrationBuilder.RenameIndex(
                name: "IX_LineOfDutyBookmarks_UserId_LineOfDutyCaseId",
                table: "LineOfDutyBookmarks",
                newName: "IX_CaseBookmarks_UserId_LineOfDutyCaseId");

            migrationBuilder.RenameIndex(
                name: "IX_LineOfDutyBookmarks_LineOfDutyCaseId",
                table: "LineOfDutyBookmarks",
                newName: "IX_CaseBookmarks_LineOfDutyCaseId");

            migrationBuilder.RenameIndex(
                name: "PK_LineOfDutyBookmarks",
                table: "LineOfDutyBookmarks",
                newName: "PK_CaseBookmarks");

            migrationBuilder.RenameTable(
                name: "LineOfDutyBookmarks",
                newName: "CaseBookmarks");
        }
    }
}
