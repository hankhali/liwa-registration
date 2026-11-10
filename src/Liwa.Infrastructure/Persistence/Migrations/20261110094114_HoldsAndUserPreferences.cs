using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liwa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HoldsAndUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                table: "users",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "walkthrough_dismissed_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hold",
                table: "student_records",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preferred_language",
                table: "users");

            migrationBuilder.DropColumn(
                name: "walkthrough_dismissed_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "hold",
                table: "student_records");
        }
    }
}
