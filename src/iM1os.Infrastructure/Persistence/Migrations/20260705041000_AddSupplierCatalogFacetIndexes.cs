using iM1os.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260705041000_AddSupplierCatalogFacetIndexes")]
    public partial class AddSupplierCatalogFacetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_Category_facet"
ON platform.global_products ("Category")
WHERE "Category" IS NOT NULL AND "Category" <> '' AND length("Category") > 1;
""", suppressTransaction: true);

            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_Brand_facet"
ON platform.global_products ("Brand")
WHERE "Brand" <> '';
""", suppressTransaction: true);

            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_Category_Brand_facet"
ON platform.global_products ("Category", "Brand")
WHERE "Category" IS NOT NULL AND "Category" <> '' AND length("Category") > 1 AND "Brand" <> '';
""", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_Category_Brand_facet";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_Brand_facet";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_Category_facet";""", suppressTransaction: true);
        }
    }
}
