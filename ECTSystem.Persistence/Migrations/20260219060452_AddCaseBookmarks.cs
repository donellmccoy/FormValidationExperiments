using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseBookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    BookmarkedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseBookmarks_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseBookmarks_LineOfDutyCaseId",
                table: "CaseBookmarks",
                column: "LineOfDutyCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseBookmarks_UserId_LineOfDutyCaseId",
                table: "CaseBookmarks",
                columns: new[] { "UserId", "LineOfDutyCaseId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseBookmarks");
        }
    }
}
