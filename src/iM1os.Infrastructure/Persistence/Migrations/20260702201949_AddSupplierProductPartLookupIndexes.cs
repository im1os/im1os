using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierProductPartLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_SupplierId_ManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products",
                columns: new[] { "SupplierId", "ManufacturerPartNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_SupplierId_SupplierPartNumber",
                schema: "platform",
                table: "supplier_products",
                columns: new[] { "SupplierId", "SupplierPartNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_products_SupplierId_ManufacturerPartNumber",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropIndex(
                name: "IX_supplier_products_SupplierId_SupplierPartNumber",
                schema: "platform",
                table: "supplier_products");
        }
    }
}
