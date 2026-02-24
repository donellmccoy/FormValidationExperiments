using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowStepHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkflowState",
                table: "TimelineSteps",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowStepHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    WorkflowState = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepHistories_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepHistories_LineOfDutyCaseId_WorkflowState",
                table: "WorkflowStepHistories",
                columns: new[] { "LineOfDutyCaseId", "WorkflowState" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowStepHistories");

            migrationBuilder.DropColumn(
                name: "WorkflowState",
                table: "TimelineSteps");
        }
    }
}
