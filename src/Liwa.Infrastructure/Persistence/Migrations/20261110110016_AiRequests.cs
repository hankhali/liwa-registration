using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liwa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_requests_at",
                table: "ai_requests",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_requests_user_id_at",
                table: "ai_requests",
                columns: new[] { "user_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_requests");
        }
    }
}
