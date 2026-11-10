using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liwa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TermInProgressOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_in_progress_prerequisites",
                table: "terms",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allow_in_progress_prerequisites",
                table: "terms");
        }
    }
}
