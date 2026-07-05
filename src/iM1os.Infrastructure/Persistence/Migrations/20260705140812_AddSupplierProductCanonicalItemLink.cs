using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierProductCanonicalItemLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalItemId",
                schema: "platform",
                table: "supplier_products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_CanonicalItemId",
                schema: "platform",
                table: "supplier_products",
                column: "CanonicalItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_products_canonical_items_CanonicalItemId",
                schema: "platform",
                table: "supplier_products",
                column: "CanonicalItemId",
                principalSchema: "platform",
                principalTable: "canonical_items",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_supplier_products_canonical_items_CanonicalItemId",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropIndex(
                name: "IX_supplier_products_CanonicalItemId",
                schema: "platform",
                table: "supplier_products");

            migrationBuilder.DropColumn(
                name: "CanonicalItemId",
                schema: "platform",
                table: "supplier_products");
        }
    }
}
