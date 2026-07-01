using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelaxGlobalProductUpcUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_global_products_Upc",
                schema: "platform",
                table: "global_products");

            migrationBuilder.CreateIndex(
                name: "IX_global_products_Upc",
                schema: "platform",
                table: "global_products",
                column: "Upc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_global_products_Upc",
                schema: "platform",
                table: "global_products");

            migrationBuilder.CreateIndex(
                name: "IX_global_products_Upc",
                schema: "platform",
                table: "global_products",
                column: "Upc",
                unique: true);
        }
    }
}
