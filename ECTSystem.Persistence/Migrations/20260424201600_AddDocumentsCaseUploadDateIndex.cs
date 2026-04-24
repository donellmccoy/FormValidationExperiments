using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentsCaseUploadDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_LineOfDutyCaseId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LineOfDutyCaseId_UploadDate_Id",
                table: "Documents",
                columns: new[] { "LineOfDutyCaseId", "UploadDate", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_LineOfDutyCaseId_UploadDate_Id",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LineOfDutyCaseId",
                table: "Documents",
                column: "LineOfDutyCaseId");
        }
    }
}
