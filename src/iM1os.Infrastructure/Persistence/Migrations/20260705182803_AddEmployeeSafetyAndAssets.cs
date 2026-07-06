using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeSafetyAndAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_company_assets",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AssetTag = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_company_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_company_assets_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_safety_incidents",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IncidentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LostTimeHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    IsOshaRecordable = table.Column<bool>(type: "boolean", nullable: false),
                    ReportedToOsha = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_safety_incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_safety_incidents_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_company_assets_EmployeeId",
                schema: "platform",
                table: "employee_company_assets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_company_assets_OrganizationId_AssetTag",
                schema: "platform",
                table: "employee_company_assets",
                columns: new[] { "OrganizationId", "AssetTag" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_company_assets_OrganizationId_EmployeeId_Status",
                schema: "platform",
                table: "employee_company_assets",
                columns: new[] { "OrganizationId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_safety_incidents_EmployeeId",
                schema: "platform",
                table: "employee_safety_incidents",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_safety_incidents_OrganizationId_EmployeeId_Inciden~",
                schema: "platform",
                table: "employee_safety_incidents",
                columns: new[] { "OrganizationId", "EmployeeId", "IncidentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_safety_incidents_OrganizationId_IncidentDate",
                schema: "platform",
                table: "employee_safety_incidents",
                columns: new[] { "OrganizationId", "IncidentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_safety_incidents_OrganizationId_IsOshaRecordable",
                schema: "platform",
                table: "employee_safety_incidents",
                columns: new[] { "OrganizationId", "IsOshaRecordable" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_company_assets",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "employee_safety_incidents",
                schema: "platform");
        }
    }
}
