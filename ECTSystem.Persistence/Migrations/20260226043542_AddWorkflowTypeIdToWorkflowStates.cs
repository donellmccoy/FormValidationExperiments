using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTypeIdToWorkflowStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStates_Name",
                table: "WorkflowStates");

            migrationBuilder.AddColumn<int>(
                name: "WorkflowTypeId",
                table: "WorkflowStates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 1,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 2,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 3,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 4,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 5,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 6,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 7,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 8,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 9,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 10,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 11,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 12,
                column: "WorkflowTypeId",
                value: 1);

            migrationBuilder.InsertData(
                table: "WorkflowStates",
                columns: new[] { "Id", "CreatedBy", "Description", "DisplayOrder", "ModifiedBy", "Name", "WorkflowTypeId" },
                values: new object[,]
                {
                    { 13, "", "Enter member identification and incident details to initiate the LOD case.", 1, "", "Member Information Entry", 2 },
                    { 14, "", "Medical technician reviews the injury/illness and documents clinical findings.", 2, "", "Medical Technician Review", 2 },
                    { 15, "", "Medical officer reviews the technician's findings and provides a clinical assessment.", 3, "", "Medical Officer Review", 2 },
                    { 16, "", "Unit commander reviews the case and submits a recommendation for the LOD determination.", 4, "", "Unit CC Review", 2 },
                    { 17, "", "Wing Judge Advocate reviews the case for legal sufficiency and compliance.", 5, "", "Wing JA Review", 2 },
                    { 18, "", "Appointing authority reviews the case and issues a formal LOD determination.", 6, "", "Appointing Authority Review", 2 },
                    { 19, "", "Wing commander reviews the case and renders a preliminary LOD determination.", 7, "", "Wing CC Review", 2 },
                    { 20, "", "Board medical technician reviews the case file for completeness and accuracy.", 8, "", "Board Technician Review", 2 },
                    { 21, "", "Board medical officer reviews all medical evidence and provides a formal assessment.", 9, "", "Board Medical Review", 2 },
                    { 22, "", "Board legal counsel reviews the case for legal sufficiency before final decision.", 10, "", "Board Legal Review", 2 },
                    { 23, "", "Board administrative officer finalizes the case package and prepares the formal determination.", 11, "", "Board Admin Review", 2 },
                    { 24, "", "LOD determination has been finalized and the case is closed.", 12, "", "Completed", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStates_Name_WorkflowTypeId",
                table: "WorkflowStates",
                columns: new[] { "Name", "WorkflowTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStates_WorkflowTypeId",
                table: "WorkflowStates",
                column: "WorkflowTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStates_WorkflowTypes_WorkflowTypeId",
                table: "WorkflowStates",
                column: "WorkflowTypeId",
                principalTable: "WorkflowTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStates_WorkflowTypes_WorkflowTypeId",
                table: "WorkflowStates");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStates_Name_WorkflowTypeId",
                table: "WorkflowStates");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStates_WorkflowTypeId",
                table: "WorkflowStates");

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DropColumn(
                name: "WorkflowTypeId",
                table: "WorkflowStates");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStates_Name",
                table: "WorkflowStates",
                column: "Name",
                unique: true);
        }
    }
}
