using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liwa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalLoad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "normal_load",
                table: "terms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "normal_load",
                table: "credit_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "normal_load",
                table: "credit_policies",
                type: "integer",
                nullable: false,
                defaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "normal_load",
                table: "terms");

            migrationBuilder.DropColumn(
                name: "normal_load",
                table: "credit_rules");

            migrationBuilder.DropColumn(
                name: "normal_load",
                table: "credit_policies");
        }
    }
}
