using iM1os.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260703211500_AddSupplierFitmentFacetIndexes")]
    public partial class AddSupplierFitmentFacetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_fitment_records_SupplierId_VehicleType_Year_Make_Model"
ON platform.supplier_fitment_records ("SupplierId", "VehicleType", "Year", "Make", "Model");
""", suppressTransaction: true);

            migrationBuilder.Sql("""
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_supplier_fitment_records_SupplierId_Year_Make_Model_VehicleType"
ON platform.supplier_fitment_records ("SupplierId", "Year", "Make", "Model", "VehicleType");
""", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_fitment_records_SupplierId_Year_Make_Model_VehicleType";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS platform."IX_supplier_fitment_records_SupplierId_VehicleType_Year_Make_Model";""", suppressTransaction: true);
        }
    }
}
