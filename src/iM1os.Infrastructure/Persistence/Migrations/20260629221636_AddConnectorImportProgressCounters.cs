using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectorImportProgressCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressProcessed",
                schema: "platform",
                table: "supplier_connector_import_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotal",
                schema: "platform",
                table: "supplier_connector_import_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressProcessed",
                schema: "platform",
                table: "company_supplier_connector_import_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotal",
                schema: "platform",
                table: "company_supplier_connector_import_runs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressProcessed",
                schema: "platform",
                table: "supplier_connector_import_runs");

            migrationBuilder.DropColumn(
                name: "ProgressTotal",
                schema: "platform",
                table: "supplier_connector_import_runs");

            migrationBuilder.DropColumn(
                name: "ProgressProcessed",
                schema: "platform",
                table: "company_supplier_connector_import_runs");

            migrationBuilder.DropColumn(
                name: "ProgressTotal",
                schema: "platform",
                table: "company_supplier_connector_import_runs");
        }
    }
}
