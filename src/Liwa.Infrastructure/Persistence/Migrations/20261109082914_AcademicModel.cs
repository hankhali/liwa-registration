using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liwa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    credits = table.Column<int>(type: "integer", nullable: false),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    prerequisites = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    offered = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credit_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    repeat_grade_rule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    allow_in_progress_prerequisites = table.Column<bool>(type: "boolean", nullable: false),
                    graduating_students_exempt_from_minimum = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    message_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: true),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "programs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    college = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    total_credits = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_programs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    season = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    teaching_days = table.Column<int>(type: "integer", nullable: false),
                    allow_friday_afternoon = table.Column<bool>(type: "boolean", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credit_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    season = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    standing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    min_gpa = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    max_gpa_exclusive = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    on_probation = table.Column<bool>(type: "boolean", nullable: true),
                    min_credits = table.Column<int>(type: "integer", nullable: false),
                    max_credits = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_credit_rules_credit_policies_credit_policy_id",
                        column: x => x.credit_policy_id,
                        principalTable: "credit_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "degree_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_degree_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_degree_plans_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_year = table.Column<int>(type: "integer", nullable: false),
                    admit_term_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    advisor_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_records_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_records_users_advisor_user_id",
                        column: x => x.advisor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_student_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    delivery = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    instructor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    room = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    enrolled = table.Column<int>(type: "integer", nullable: false),
                    waitlist = table.Column<bool>(type: "boolean", nullable: false),
                    meetings = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sections", x => x.id);
                    table.ForeignKey(
                        name: "fk_sections_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sections_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "requirement_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    degree_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    credits_required = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requirement_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_requirement_groups_degree_plans_degree_plan_id",
                        column: x => x.degree_plan_id,
                        principalTable: "degree_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_plans_student_records_student_record_id",
                        column: x => x.student_record_id,
                        principalTable: "student_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plans_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transcript_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    credits = table.Column<int>(type: "integer", nullable: false),
                    grade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transcript_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_transcript_entries_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transcript_entries_student_records_student_record_id",
                        column: x => x.student_record_id,
                        principalTable: "student_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfer_credits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credits = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_credits", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfer_credits_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfer_credits_student_records_student_record_id",
                        column: x => x.student_record_id,
                        principalTable: "student_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requirement_courses",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_semester = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requirement_courses", x => new { x.group_id, x.course_id });
                    table.ForeignKey(
                        name: "fk_requirement_courses_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_requirement_courses_requirement_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "requirement_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_versions_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plan_versions_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_version_sections",
                columns: table => new
                {
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_version_sections", x => new { x.plan_version_id, x.section_id });
                    table.ForeignKey(
                        name: "fk_plan_version_sections_plan_versions_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plan_version_sections_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_at",
                table: "audit_log",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity_type_entity_id",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_code",
                table: "courses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_rules_credit_policy_id_priority",
                table: "credit_rules",
                columns: new[] { "credit_policy_id", "priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_degree_plans_program_id_catalog_year",
                table: "degree_plans",
                columns: new[] { "program_id", "catalog_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at",
                table: "notifications",
                columns: new[] { "user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_version_sections_section_id",
                table: "plan_version_sections",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_versions_author_user_id",
                table: "plan_versions",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_versions_plan_id_number",
                table: "plan_versions",
                columns: new[] { "plan_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_status",
                table: "plans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_plans_student_record_id_term_id",
                table: "plans",
                columns: new[] { "student_record_id", "term_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_term_id",
                table: "plans",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ix_programs_code",
                table: "programs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_requirement_courses_course_id",
                table: "requirement_courses",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_requirement_groups_degree_plan_id_key",
                table: "requirement_groups",
                columns: new[] { "degree_plan_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_course_id",
                table: "sections",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_sections_term_id_course_id_section_code",
                table: "sections",
                columns: new[] { "term_id", "course_id", "section_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_records_advisor_user_id",
                table: "student_records",
                column: "advisor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_records_program_id",
                table: "student_records",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_records_student_id",
                table: "student_records",
                column: "student_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_records_user_id",
                table: "student_records",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terms_code",
                table: "terms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcript_entries_course_id",
                table: "transcript_entries",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_transcript_entries_student_record_id",
                table: "transcript_entries",
                column: "student_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_credits_course_id",
                table: "transfer_credits",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_credits_student_record_id",
                table: "transfer_credits",
                column: "student_record_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "credit_rules");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "plan_version_sections");

            migrationBuilder.DropTable(
                name: "requirement_courses");

            migrationBuilder.DropTable(
                name: "transcript_entries");

            migrationBuilder.DropTable(
                name: "transfer_credits");

            migrationBuilder.DropTable(
                name: "credit_policies");

            migrationBuilder.DropTable(
                name: "plan_versions");

            migrationBuilder.DropTable(
                name: "sections");

            migrationBuilder.DropTable(
                name: "requirement_groups");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "degree_plans");

            migrationBuilder.DropTable(
                name: "student_records");

            migrationBuilder.DropTable(
                name: "terms");

            migrationBuilder.DropTable(
                name: "programs");
        }
    }
}
