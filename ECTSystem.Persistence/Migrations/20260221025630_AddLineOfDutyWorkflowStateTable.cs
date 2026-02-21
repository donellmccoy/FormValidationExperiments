using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLineOfDutyWorkflowStateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LineOfDutyWorkflowState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineOfDutyWorkflowState", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LineOfDutyWorkflowState",
                columns: new[] { "Id", "Description", "DisplayOrder", "Name" },
                values: new object[,]
                {
                    { 1, "Enter member identification and incident details to initiate the LOD case.", 1, "Member Information Entry" },
                    { 2, "Medical technician reviews the injury/illness and documents clinical findings.", 2, "Medical Technician Review" },
                    { 3, "Medical officer reviews the technician's findings and provides a clinical assessment.", 3, "Medical Officer Review" },
                    { 4, "Unit commander reviews the case and submits a recommendation for the LOD determination.", 4, "Unit CC Review" },
                    { 5, "Wing Judge Advocate reviews the case for legal sufficiency and compliance.", 5, "Wing JA Review" },
                    { 6, "Appointing authority reviews the case and issues a formal LOD determination.", 6, "Appointing Authority Review" },
                    { 7, "Wing commander reviews the case and renders a preliminary LOD determination.", 7, "Wing CC Review" },
                    { 8, "Board medical technician reviews the case file for completeness and accuracy.", 8, "Board Technician Review" },
                    { 9, "Board medical officer reviews all medical evidence and provides a formal assessment.", 9, "Board Medical Review" },
                    { 10, "Board legal counsel reviews the case for legal sufficiency before final decision.", 10, "Board Legal Review" },
                    { 11, "Board administrative officer finalizes the case package and prepares the formal determination.", 11, "Board Admin Review" },
                    { 12, "LOD determination has been finalized and the case is closed.", 12, "Completed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineOfDutyWorkflowState_Name",
                table: "LineOfDutyWorkflowState",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LineOfDutyWorkflowState");
        }
    }
}
