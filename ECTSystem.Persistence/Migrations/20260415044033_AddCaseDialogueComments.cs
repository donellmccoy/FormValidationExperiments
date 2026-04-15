using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseDialogueComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "Documents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "CaseDialogueComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuthorRole = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDialogueComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseDialogueComments_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDialogueComments_LineOfDutyCaseId_CreatedDate",
                table: "CaseDialogueComments",
                columns: new[] { "LineOfDutyCaseId", "CreatedDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDialogueComments_ParentCommentId",
                table: "CaseDialogueComments",
                column: "ParentCommentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseDialogueComments");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "Documents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
