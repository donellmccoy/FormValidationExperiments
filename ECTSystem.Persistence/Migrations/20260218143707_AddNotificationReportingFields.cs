using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationReportingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifiedMedicalUnitTimely",
                table: "Cases",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubmittedMedicalDocumentsTimely",
                table: "Cases",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifiedMedicalUnitTimely",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SubmittedMedicalDocumentsTimely",
                table: "Cases");
        }
    }
}
