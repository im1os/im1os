using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeJobFunctionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAccounting",
                schema: "platform",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInventory",
                schema: "platform",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManager",
                schema: "platform",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsParts",
                schema: "platform",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSales",
                schema: "platform",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsServiceAdvisor",
                schema: "platform",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTechnician",
                schema: "platform",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAccounting",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "IsInventory",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "IsManager",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "IsParts",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "IsSales",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "IsServiceAdvisor",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "IsTechnician",
                schema: "platform",
                table: "employees");
        }
    }
}
