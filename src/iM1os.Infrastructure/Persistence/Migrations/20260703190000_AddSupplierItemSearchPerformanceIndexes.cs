using iM1os.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260703190000_AddSupplierItemSearchPerformanceIndexes")]
    public partial class AddSupplierItemSearchPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pg_trgm;""");

            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_products_SupplierSku_trgm"
ON platform.supplier_products USING gin ("SupplierSku" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_products_SupplierPartNumber_trgm"
ON platform.supplier_products USING gin ("SupplierPartNumber" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_products_ManufacturerPartNumber_trgm"
ON platform.supplier_products USING gin ("ManufacturerPartNumber" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_products_NormalizedManufacturerPartNumber"
ON platform.supplier_products ("NormalizedManufacturerPartNumber");
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_products_SupplierDescription_trgm"
ON platform.supplier_products USING gin ("SupplierDescription" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_Brand_trgm"
ON platform.global_products USING gin ("Brand" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_Manufacturer_trgm"
ON platform.global_products USING gin ("Manufacturer" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_ManufacturerPartNumber_trgm"
ON platform.global_products USING gin ("ManufacturerPartNumber" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_NormalizedManufacturerPartNumber"
ON platform.global_products ("NormalizedManufacturerPartNumber");
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_Description_trgm"
ON platform.global_products USING gin ("Description" gin_trgm_ops);
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_global_products_LongDescription_trgm"
ON platform.global_products USING gin ("LongDescription" gin_trgm_ops);
""", suppressTransaction: true);

            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_fitment_records_Year_Make_Model_VehicleType"
ON platform.supplier_fitment_records ("Year", "Make", "Model", "VehicleType");
""", suppressTransaction: true);
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_fitment_records_VehicleType_Year_Make_Model"
ON platform.supplier_fitment_records ("VehicleType", "Year", "Make", "Model");
""", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_fitment_records_Year_Make_Model_VehicleType";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_fitment_records_VehicleType_Year_Make_Model";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_LongDescription_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_Description_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_NormalizedManufacturerPartNumber";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_ManufacturerPartNumber_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_Manufacturer_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_global_products_Brand_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_products_SupplierDescription_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_products_NormalizedManufacturerPartNumber";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_products_ManufacturerPartNumber_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_products_SupplierPartNumber_trgm";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_products_SupplierSku_trgm";""", suppressTransaction: true);
        }
    }
}
