using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCompensationAndPinIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_compensations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    SalaryAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    WorkOrderCommissionRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    SalesCommissionRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    EffectiveStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_compensations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_compensations_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_OrganizationId_PinHash",
                schema: "platform",
                table: "users",
                columns: new[] { "OrganizationId", "PinHash" },
                unique: true,
                filter: "\"PinHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_employee_compensations_EmployeeId",
                schema: "platform",
                table: "employee_compensations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_compensations_OrganizationId_EmployeeId_EffectiveS~",
                schema: "platform",
                table: "employee_compensations",
                columns: new[] { "OrganizationId", "EmployeeId", "EffectiveStartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_compensations",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_users_OrganizationId_PinHash",
                schema: "platform",
                table: "users");
        }
    }
}
