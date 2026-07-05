using iM1os.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260704152500_AddSupplierCatalogFullTextSearchIndexes")]
    public partial class AddSupplierCatalogFullTextSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_products_catalog_search_vector"
ON platform.supplier_products USING gin (
    to_tsvector('simple',
        coalesce("SupplierSku", '') || ' ' ||
        coalesce("SupplierPartNumber", '') || ' ' ||
        coalesce("ManufacturerPartNumber", '') || ' ' ||
        coalesce("SupplierDescription", '')
    )
);
""", suppressTransaction: true);

            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_catalog_search_vector"
ON platform.global_products USING gin (
    to_tsvector('simple',
        coalesce("ManufacturerPartNumber", '') || ' ' ||
        coalesce("Manufacturer", '') || ' ' ||
        coalesce("Brand", '') || ' ' ||
        coalesce("Description", '') || ' ' ||
        coalesce("Category", '') || ' ' ||
        coalesce("LongDescription", '')
    )
);
""", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_catalog_search_vector";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_products_catalog_search_vector";""", suppressTransaction: true);
        }
    }
}
