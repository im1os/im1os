using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeMasterRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                schema: "platform",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employees",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    JobTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Department = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO platform.employees (
                    "Id",
                    "OrganizationId",
                    "FirstName",
                    "LastName",
                    "DisplayName",
                    "Email",
                    "Phone",
                    "JobTitle",
                    "EmploymentType",
                    "Status",
                    "DeletedAtUtc",
                    "CreatedAtUtc",
                    "CreatedByUserId",
                    "UpdatedAtUtc",
                    "UpdatedByUserId")
                SELECT
                    u."Id",
                    u."OrganizationId",
                    u."FirstName",
                    u."LastName",
                    u."DisplayName",
                    u."Email",
                    u."Phone",
                    u."JobTitle",
                    'Employee',
                    CASE
                        WHEN u."DeletedAtUtc" IS NOT NULL THEN 'Inactive'
                        WHEN u."IsActive" THEN 'Active'
                        ELSE 'Inactive'
                    END,
                    u."DeletedAtUtc",
                    u."CreatedAtUtc",
                    u."CreatedByUserId",
                    u."UpdatedAtUtc",
                    u."UpdatedByUserId"
                FROM platform.users u
                WHERE u."EmployeeId" IS NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM platform.employees e
                    WHERE e."Id" = u."Id"
                  );

                UPDATE platform.users u
                SET "EmployeeId" = u."Id"
                WHERE u."EmployeeId" IS NULL
                  AND EXISTS (
                    SELECT 1
                    FROM platform.employees e
                    WHERE e."Id" = u."Id"
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_EmployeeId",
                schema: "platform",
                table: "users",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_OrganizationId_DisplayName",
                schema: "platform",
                table: "employees",
                columns: new[] { "OrganizationId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_OrganizationId_Email",
                schema: "platform",
                table: "employees",
                columns: new[] { "OrganizationId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_OrganizationId_EmployeeNumber",
                schema: "platform",
                table: "employees",
                columns: new[] { "OrganizationId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_users_employees_EmployeeId",
                schema: "platform",
                table: "users",
                column: "EmployeeId",
                principalSchema: "platform",
                principalTable: "employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_employees_EmployeeId",
                schema: "platform",
                table: "users");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_users_EmployeeId",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                schema: "platform",
                table: "users");
        }
    }
}
