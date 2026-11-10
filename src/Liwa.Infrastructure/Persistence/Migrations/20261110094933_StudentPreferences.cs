using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liwa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    availability = table.Column<string>(type: "text", nullable: true),
                    delivery = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_credits = table.Column<int>(type: "integer", nullable: true),
                    draft_section_ids = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_student_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_preferences");
        }
    }
}
