using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "global_products",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LongDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Upc = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Length = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Width = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Height = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    ImagesJson = table.Column<string>(type: "jsonb", nullable: true),
                    SpecificationsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "global_vehicles",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Make = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Submodel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Engine = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    VinRange = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Market = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConnectorKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_fitments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalVehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_fitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_fitments_global_products_GlobalProductId",
                        column: x => x.GlobalProductId,
                        principalSchema: "platform",
                        principalTable: "global_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vehicle_fitments_global_vehicles_GlobalVehicleId",
                        column: x => x.GlobalVehicleId,
                        principalSchema: "platform",
                        principalTable: "global_vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_match_review_items",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SupplierPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Upc = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SupplierDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CandidateGlobalProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_match_review_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_match_review_items_global_products_CandidateGlobalP~",
                        column: x => x.CandidateGlobalProductId,
                        principalSchema: "platform",
                        principalTable: "global_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_product_match_review_items_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_products",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SupplierDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupplierPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SupplierStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Packaging = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    MinimumOrder = table.Column<int>(type: "integer", nullable: true),
                    CaseQuantity = table.Column<int>(type: "integer", nullable: true),
                    WarehouseAvailability = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupplierImagesJson = table.Column<string>(type: "jsonb", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_products_global_products_GlobalProductId",
                        column: x => x.GlobalProductId,
                        principalSchema: "platform",
                        principalTable: "global_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_products_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_prices",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Msrp = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Map = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    DealerCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_prices_supplier_products_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_global_products_Brand_ManufacturerPartNumber",
                schema: "platform",
                table: "global_products",
                columns: new[] { "Brand", "ManufacturerPartNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_global_products_Description",
                schema: "platform",
                table: "global_products",
                column: "Description");

            migrationBuilder.CreateIndex(
                name: "IX_global_products_Upc",
                schema: "platform",
                table: "global_products",
                column: "Upc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_global_vehicles_Year_Make_Model_Submodel_Engine_Market",
                schema: "platform",
                table: "global_vehicles",
                columns: new[] { "Year", "Make", "Model", "Submodel", "Engine", "Market" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_match_review_items_CandidateGlobalProductId",
                schema: "platform",
                table: "product_match_review_items",
                column: "CandidateGlobalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_match_review_items_SupplierId_SupplierSku_Status",
                schema: "platform",
                table: "product_match_review_items",
                columns: new[] { "SupplierId", "SupplierSku", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_prices_SupplierProductId_EffectiveDate",
                schema: "platform",
                table: "supplier_prices",
                columns: new[] { "SupplierProductId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_GlobalProductId",
                schema: "platform",
                table: "supplier_products",
                column: "GlobalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_SupplierId_SupplierSku",
                schema: "platform",
                table: "supplier_products",
                columns: new[] { "SupplierId", "SupplierSku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_SupplierPartNumber",
                schema: "platform",
                table: "supplier_products",
                column: "SupplierPartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_Code",
                schema: "platform",
                table: "suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_fitments_GlobalProductId_GlobalVehicleId_Position",
                schema: "platform",
                table: "vehicle_fitments",
                columns: new[] { "GlobalProductId", "GlobalVehicleId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_fitments_GlobalVehicleId",
                schema: "platform",
                table: "vehicle_fitments",
                column: "GlobalVehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_match_review_items",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "supplier_prices",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "vehicle_fitments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "supplier_products",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "global_vehicles",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "global_products",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "platform");
        }
    }
}
