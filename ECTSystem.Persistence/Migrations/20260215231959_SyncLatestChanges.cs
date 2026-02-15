using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncLatestChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases");

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases");

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Members_MemberId",
                table: "Cases",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id");
        }
    }
}
