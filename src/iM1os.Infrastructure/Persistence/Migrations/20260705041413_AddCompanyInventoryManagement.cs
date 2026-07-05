using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInventoryManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_inventory_items",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceSupplierCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceSupplierName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SourceSupplierSku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceSupplierProductId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Sku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NormalizedManufacturerPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Upc = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Subcategory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetailPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    DefaultCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    AverageCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    LastCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    IsStockedInStore = table.Column<bool>(type: "boolean", nullable: false),
                    TrackInventory = table.Column<bool>(type: "boolean", nullable: false),
                    IsSerialized = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_inventory_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_inventory_items_global_products_GlobalProductId",
                        column: x => x.GlobalProductId,
                        principalSchema: "platform",
                        principalTable: "global_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_company_inventory_items_supplier_products_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "company_inventory_location_stocks",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyInventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BinLocation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    QuantityAllocated = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    QuantityAvailable = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    QuantityOnOrder = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    QuantityBackordered = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MinQuantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    MaxQuantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ReorderPoint = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    StockInStore = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNegativeStock = table.Column<bool>(type: "boolean", nullable: false),
                    LastCountedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_inventory_location_stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_inventory_location_stocks_company_inventory_items_C~",
                        column: x => x.CompanyInventoryItemId,
                        principalSchema: "platform",
                        principalTable: "company_inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_inventory_location_stocks_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "platform",
                        principalTable: "locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "company_inventory_movements",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyInventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Reason = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_inventory_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_inventory_movements_company_inventory_items_Company~",
                        column: x => x.CompanyInventoryItemId,
                        principalSchema: "platform",
                        principalTable: "company_inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_inventory_movements_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "platform",
                        principalTable: "locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_GlobalProductId",
                schema: "platform",
                table: "company_inventory_items",
                column: "GlobalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_Brand",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "Brand" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_Category",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_ManufacturerPartNumb~",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "ManufacturerPartNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_NormalizedManufactur~",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "NormalizedManufacturerPartNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_Sku",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_Status",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_SupplierProductId",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "SupplierProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_OrganizationId_Upc",
                schema: "platform",
                table: "company_inventory_items",
                columns: new[] { "OrganizationId", "Upc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_items_SupplierProductId",
                schema: "platform",
                table: "company_inventory_items",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_location_stocks_CompanyInventoryItemId",
                schema: "platform",
                table: "company_inventory_location_stocks",
                column: "CompanyInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_location_stocks_LocationId",
                schema: "platform",
                table: "company_inventory_location_stocks",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_location_stocks_OrganizationId_CompanyInv~",
                schema: "platform",
                table: "company_inventory_location_stocks",
                columns: new[] { "OrganizationId", "CompanyInventoryItemId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_location_stocks_OrganizationId_LocationId",
                schema: "platform",
                table: "company_inventory_location_stocks",
                columns: new[] { "OrganizationId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_location_stocks_OrganizationId_StockInSto~",
                schema: "platform",
                table: "company_inventory_location_stocks",
                columns: new[] { "OrganizationId", "StockInStore" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_movements_CompanyInventoryItemId",
                schema: "platform",
                table: "company_inventory_movements",
                column: "CompanyInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_movements_LocationId",
                schema: "platform",
                table: "company_inventory_movements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_movements_OrganizationId_CompanyInventory~",
                schema: "platform",
                table: "company_inventory_movements",
                columns: new[] { "OrganizationId", "CompanyInventoryItemId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_inventory_movements_OrganizationId_LocationId_Creat~",
                schema: "platform",
                table: "company_inventory_movements",
                columns: new[] { "OrganizationId", "LocationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_inventory_location_stocks",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "company_inventory_movements",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "company_inventory_items",
                schema: "platform");
        }
    }
}
