using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECTSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateIdentityContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: Identity tables are now managed by EctIdentityDbContext.
            // This migration only updates the EctDbContext model snapshot.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: reverting would re-add Identity tables to this context's
            // snapshot, but they are already managed by EctIdentityDbContext.
        }
    }
}
