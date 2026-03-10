using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTimelineStepsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimelineSteps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimelineSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    ResponsibleAuthorityId = table.Column<int>(type: "int", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StepDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimelineDays = table.Column<int>(type: "int", nullable: false),
                    WorkflowState = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimelineSteps_Authorities_ResponsibleAuthorityId",
                        column: x => x.ResponsibleAuthorityId,
                        principalTable: "Authorities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TimelineSteps_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSteps_LineOfDutyCaseId",
                table: "TimelineSteps",
                column: "LineOfDutyCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSteps_ResponsibleAuthorityId",
                table: "TimelineSteps",
                column: "ResponsibleAuthorityId");
        }
    }
}
