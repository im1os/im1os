using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierProductSourceItemId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceSupplierProductId",
                schema: "platform",
                table: "supplier_products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_SupplierId_SourceSupplierProductId",
                schema: "platform",
                table: "supplier_products",
                columns: new[] { "SupplierId", "SourceSupplierProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_products_SupplierId_SourceSupplierProductId",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropColumn(
                name: "SourceSupplierProductId",
                schema: "platform",
                table: "supplier_products");
        }
    }
}
