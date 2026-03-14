using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromoteStringListsToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuditComments",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WitnessStatements",
                table: "Cases");

            migrationBuilder.CreateTable(
                name: "AuditComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditComments_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WitnessStatements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WitnessStatements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WitnessStatements_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditComments_LineOfDutyCaseId",
                table: "AuditComments",
                column: "LineOfDutyCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_WitnessStatements_LineOfDutyCaseId",
                table: "WitnessStatements",
                column: "LineOfDutyCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditComments");

            migrationBuilder.DropTable(
                name: "WitnessStatements");

            migrationBuilder.AddColumn<string>(
                name: "AuditComments",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessStatements",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
