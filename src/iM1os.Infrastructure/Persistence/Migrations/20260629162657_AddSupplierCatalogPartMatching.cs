using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCatalogPartMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MasterFileUrl",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "global_products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products",
                column: "NormalizedManufacturerPartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_SupplierId_NormalizedManufacturerPartNumb~",
                schema: "platform",
                table: "supplier_products",
                columns: new[] { "SupplierId", "NormalizedManufacturerPartNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_global_products_Brand_NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "global_products",
                columns: new[] { "Brand", "NormalizedManufacturerPartNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_products_NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropIndex(
                name: "IX_supplier_products_SupplierId_NormalizedManufacturerPartNumb~",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropIndex(
                name: "IX_global_products_Brand_NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "ManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropColumn(
                name: "NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropColumn(
                name: "MasterFileUrl",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "global_products");
        }
    }
}
