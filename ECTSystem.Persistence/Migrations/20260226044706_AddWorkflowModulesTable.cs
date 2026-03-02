using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowModulesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowTypes_Name",
                table: "WorkflowTypes");

            migrationBuilder.AddColumn<int>(
                name: "WorkflowModuleId",
                table: "WorkflowTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WorkflowModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowModules", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "WorkflowModules",
                columns: new[] { "Id", "CreatedBy", "Description", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, "", "Air Force Reserve Command workflow module.", "", "AFRC" },
                    { 2, "", "Air National Guard workflow module.", "", "ANG" }
                });

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "WorkflowModuleId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "WorkflowModuleId",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTypes_Name_WorkflowModuleId",
                table: "WorkflowTypes",
                columns: new[] { "Name", "WorkflowModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTypes_WorkflowModuleId",
                table: "WorkflowTypes",
                column: "WorkflowModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowModules_Name",
                table: "WorkflowModules",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTypes_WorkflowModules_WorkflowModuleId",
                table: "WorkflowTypes",
                column: "WorkflowModuleId",
                principalTable: "WorkflowModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTypes_WorkflowModules_WorkflowModuleId",
                table: "WorkflowTypes");

            migrationBuilder.DropTable(
                name: "WorkflowModules");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTypes_Name_WorkflowModuleId",
                table: "WorkflowTypes");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTypes_WorkflowModuleId",
                table: "WorkflowTypes");

            migrationBuilder.DropColumn(
                name: "WorkflowModuleId",
                table: "WorkflowTypes");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTypes_Name",
                table: "WorkflowTypes",
                column: "Name",
                unique: true);
        }
    }
}
