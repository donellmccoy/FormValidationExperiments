using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAuditByFieldsToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear existing nvarchar data that cannot be converted to int.
            // All affected tables get CreatedBy/ModifiedBy set to 0 before the column type change.
            var tables = new[]
            {
                "WorkflowTypes", "WorkflowStates", "WorkflowStateHistories", "WorkflowModules",
                "Notifications", "Members", "MEDCONDetails", "LineOfDutyBookmarks", "INCAPDetails",
                "Documents", "Cases", "Authorities", "Appeals", "WitnessStatements", "AuditComments"
            };

            foreach (var table in tables)
            {
                migrationBuilder.Sql(
                    $"IF COL_LENGTH('{table}', 'CreatedBy') IS NOT NULL " +
                    $"EXEC sp_executesql N'UPDATE [{table}] SET [CreatedBy] = ''0'' WHERE [CreatedBy] IS NULL OR ISNUMERIC([CreatedBy]) = 0'");
                migrationBuilder.Sql(
                    $"IF COL_LENGTH('{table}', 'ModifiedBy') IS NOT NULL " +
                    $"EXEC sp_executesql N'UPDATE [{table}] SET [ModifiedBy] = ''0'' WHERE [ModifiedBy] IS NULL OR ISNUMERIC([ModifiedBy]) = 0'");
            }

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "WorkflowTypes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "WorkflowTypes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "WorkflowStates",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "WorkflowStates",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "WorkflowStateHistories",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "WorkflowStateHistories",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "WorkflowModules",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "WorkflowModules",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "Members",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Members",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "MEDCONDetails",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "MEDCONDetails",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "LineOfDutyBookmarks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "LineOfDutyBookmarks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "INCAPDetails",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "INCAPDetails",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "Authorities",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Authorities",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModifiedBy",
                table: "Appeals",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Appeals",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "WorkflowModules",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowModules",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { 0, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "WorkflowTypes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "WorkflowTypes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "WorkflowStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "WorkflowStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "WorkflowStateHistories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "WorkflowStateHistories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "WorkflowModules",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "WorkflowModules",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "MEDCONDetails",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "MEDCONDetails",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "LineOfDutyBookmarks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "LineOfDutyBookmarks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "INCAPDetails",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "INCAPDetails",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Authorities",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Authorities",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Appeals",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Appeals",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.UpdateData(
                table: "WorkflowModules",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowModules",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowStates",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "WorkflowTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { "", "" });
        }
    }
}
