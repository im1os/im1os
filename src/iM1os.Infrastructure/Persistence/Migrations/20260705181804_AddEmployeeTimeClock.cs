using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeTimeClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_schedule_shifts",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_schedule_shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_schedule_shifts_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_time_off_requests",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HoursPerDay = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_time_off_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_time_off_requests_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_time_punches",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClockInUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClockOutUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsManualEntry = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_time_punches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_time_punches_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_schedule_shifts_EmployeeId",
                schema: "platform",
                table: "employee_schedule_shifts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_schedule_shifts_OrganizationId_EmployeeId_ShiftDate",
                schema: "platform",
                table: "employee_schedule_shifts",
                columns: new[] { "OrganizationId", "EmployeeId", "ShiftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_time_off_requests_EmployeeId",
                schema: "platform",
                table: "employee_time_off_requests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_time_off_requests_OrganizationId_EmployeeId_StartD~",
                schema: "platform",
                table: "employee_time_off_requests",
                columns: new[] { "OrganizationId", "EmployeeId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_time_off_requests_OrganizationId_Status",
                schema: "platform",
                table: "employee_time_off_requests",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_time_punches_EmployeeId",
                schema: "platform",
                table: "employee_time_punches",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_time_punches_OrganizationId_EmployeeId_ClockInUtc",
                schema: "platform",
                table: "employee_time_punches",
                columns: new[] { "OrganizationId", "EmployeeId", "ClockInUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_time_punches_OrganizationId_EmployeeId_ClockOutUtc",
                schema: "platform",
                table: "employee_time_punches",
                columns: new[] { "OrganizationId", "EmployeeId", "ClockOutUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_schedule_shifts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "employee_time_off_requests",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "employee_time_punches",
                schema: "platform");
        }
    }
}
