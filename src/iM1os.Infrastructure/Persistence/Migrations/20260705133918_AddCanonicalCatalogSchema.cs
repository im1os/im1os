using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalCatalogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canonical_items",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NormalizedManufacturerPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Subcategory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PrimaryUpc = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PrimaryImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "canonical_fitments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Make = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Submodel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Engine = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_fitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_fitments_canonical_items_CanonicalItemId",
                        column: x => x.CanonicalItemId,
                        principalSchema: "platform",
                        principalTable: "canonical_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_item_identifiers",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentifierType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdentifierValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_item_identifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_item_identifiers_canonical_items_CanonicalItemId",
                        column: x => x.CanonicalItemId,
                        principalSchema: "platform",
                        principalTable: "canonical_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_item_identifiers_supplier_products_SupplierProduc~",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_canonical_item_identifiers_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "canonical_item_sources",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SourceTable = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MatchMethod = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MatchConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_item_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_item_sources_canonical_items_CanonicalItemId",
                        column: x => x.CanonicalItemId,
                        principalSchema: "platform",
                        principalTable: "canonical_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_item_sources_global_products_GlobalProductId",
                        column: x => x.GlobalProductId,
                        principalSchema: "platform",
                        principalTable: "global_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_canonical_item_sources_supplier_products_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_canonical_item_sources_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "canonical_item_supplier_offers",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SupplierPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SupplierTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ListPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    DealerCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    WarehouseAvailability = table.Column<string>(type: "jsonb", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_item_supplier_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_item_supplier_offers_canonical_items_CanonicalIte~",
                        column: x => x.CanonicalItemId,
                        principalSchema: "platform",
                        principalTable: "canonical_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_item_supplier_offers_supplier_products_SupplierPr~",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_item_supplier_offers_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_CanonicalItemId",
                schema: "platform",
                table: "canonical_fitments",
                column: "CanonicalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_CanonicalItemId_Year_Make_Model_Submodel~",
                schema: "platform",
                table: "canonical_fitments",
                columns: new[] { "CanonicalItemId", "Year", "Make", "Model", "Submodel", "Engine" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_VehicleType",
                schema: "platform",
                table: "canonical_fitments",
                column: "VehicleType");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_Year_Make_Model",
                schema: "platform",
                table: "canonical_fitments",
                columns: new[] { "Year", "Make", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_identifiers_CanonicalItemId",
                schema: "platform",
                table: "canonical_item_identifiers",
                column: "CanonicalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_identifiers_IdentifierType_NormalizedValue",
                schema: "platform",
                table: "canonical_item_identifiers",
                columns: new[] { "IdentifierType", "NormalizedValue" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_identifiers_SupplierId_IdentifierType_Normal~",
                schema: "platform",
                table: "canonical_item_identifiers",
                columns: new[] { "SupplierId", "IdentifierType", "NormalizedValue" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_identifiers_SupplierProductId",
                schema: "platform",
                table: "canonical_item_identifiers",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_sources_CanonicalItemId",
                schema: "platform",
                table: "canonical_item_sources",
                column: "CanonicalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_sources_GlobalProductId",
                schema: "platform",
                table: "canonical_item_sources",
                column: "GlobalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_sources_SourceTable_SourceKey",
                schema: "platform",
                table: "canonical_item_sources",
                columns: new[] { "SourceTable", "SourceKey" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_sources_SupplierId",
                schema: "platform",
                table: "canonical_item_sources",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_sources_SupplierProductId",
                schema: "platform",
                table: "canonical_item_sources",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_supplier_offers_CanonicalItemId",
                schema: "platform",
                table: "canonical_item_supplier_offers",
                column: "CanonicalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_supplier_offers_CanonicalItemId_SupplierCode",
                schema: "platform",
                table: "canonical_item_supplier_offers",
                columns: new[] { "CanonicalItemId", "SupplierCode" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_supplier_offers_SupplierId_SupplierSku",
                schema: "platform",
                table: "canonical_item_supplier_offers",
                columns: new[] { "SupplierId", "SupplierSku" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_supplier_offers_SupplierProductId",
                schema: "platform",
                table: "canonical_item_supplier_offers",
                column: "SupplierProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canonical_items_Category",
                schema: "platform",
                table: "canonical_items",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_items_IsActive",
                schema: "platform",
                table: "canonical_items",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_items_NormalizedManufacturerPartNumber_Brand",
                schema: "platform",
                table: "canonical_items",
                columns: new[] { "NormalizedManufacturerPartNumber", "Brand" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_items_PrimaryUpc",
                schema: "platform",
                table: "canonical_items",
                column: "PrimaryUpc");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_items_Status",
                schema: "platform",
                table: "canonical_items",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canonical_fitments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "canonical_item_identifiers",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "canonical_item_sources",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "canonical_item_supplier_offers",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "canonical_items",
                schema: "platform");
        }
    }
}
