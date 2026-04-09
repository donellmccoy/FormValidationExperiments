using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeIncapMedconFKsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cases_INCAPId",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_MEDCONId",
                table: "Cases");

            migrationBuilder.AlterColumn<int>(
                name: "MEDCONId",
                table: "Cases",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "INCAPId",
                table: "Cases",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_INCAPId",
                table: "Cases",
                column: "INCAPId",
                unique: true,
                filter: "[INCAPId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_MEDCONId",
                table: "Cases",
                column: "MEDCONId",
                unique: true,
                filter: "[MEDCONId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cases_INCAPId",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_MEDCONId",
                table: "Cases");

            migrationBuilder.AlterColumn<int>(
                name: "MEDCONId",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "INCAPId",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_INCAPId",
                table: "Cases",
                column: "INCAPId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_MEDCONId",
                table: "Cases",
                column: "MEDCONId",
                unique: true);
        }
    }
}
