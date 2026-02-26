using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseClientCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseBookmarks_Cases_LineOfDutyCaseId",
                table: "CaseBookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStateHistories_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseBookmarks_Cases_LineOfDutyCaseId",
                table: "CaseBookmarks",
                column: "LineOfDutyCaseId",
                principalTable: "Cases",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStateHistories_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistories",
                column: "LineOfDutyCaseId",
                principalTable: "Cases",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseBookmarks_Cases_LineOfDutyCaseId",
                table: "CaseBookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStateHistories_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseBookmarks_Cases_LineOfDutyCaseId",
                table: "CaseBookmarks",
                column: "LineOfDutyCaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStateHistories_Cases_LineOfDutyCaseId",
                table: "WorkflowStateHistories",
                column: "LineOfDutyCaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
