using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalSearchFastPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_identifiers_NormalizedValue",
                schema: "platform",
                table: "canonical_item_identifiers",
                column: "NormalizedValue");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_supplier_offers_SupplierPartNumber",
                schema: "platform",
                table: "canonical_item_supplier_offers",
                column: "SupplierPartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_supplier_offers_SupplierSku",
                schema: "platform",
                table: "canonical_item_supplier_offers",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_items_NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "canonical_items",
                column: "NormalizedManufacturerPartNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_canonical_item_identifiers_NormalizedValue",
                schema: "platform",
                table: "canonical_item_identifiers");

            migrationBuilder.DropIndex(
                name: "IX_canonical_item_supplier_offers_SupplierPartNumber",
                schema: "platform",
                table: "canonical_item_supplier_offers");

            migrationBuilder.DropIndex(
                name: "IX_canonical_item_supplier_offers_SupplierSku",
                schema: "platform",
                table: "canonical_item_supplier_offers");

            migrationBuilder.DropIndex(
                name: "IX_canonical_items_NormalizedManufacturerPartNumber",
                schema: "platform",
                table: "canonical_items");
        }
    }
}
