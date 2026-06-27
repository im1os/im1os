using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartsEngineFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Height",
                schema: "platform",
                table: "manufacturer_parts",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                schema: "platform",
                table: "manufacturer_parts",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Map",
                schema: "platform",
                table: "manufacturer_parts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Msrp",
                schema: "platform",
                table: "manufacturer_parts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "platform",
                table: "manufacturer_parts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subcategory",
                schema: "platform",
                table: "manufacturer_parts",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByManufacturerPartId",
                schema: "platform",
                table: "manufacturer_parts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                schema: "platform",
                table: "manufacturer_parts",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Width",
                schema: "platform",
                table: "manufacturer_parts",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManufacturerPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    BinLocation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                    QuantityAllocated = table.Column<int>(type: "integer", nullable: false),
                    QuantityAvailable = table.Column<int>(type: "integer", nullable: false),
                    AverageCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    LastCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ReorderPoint = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_items_manufacturer_parts_ManufacturerPartId",
                        column: x => x.ManufacturerPartId,
                        principalSchema: "platform",
                        principalTable: "manufacturer_parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "manufacturer_part_cross_references",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturerPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ReferenceValue = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturer_part_cross_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturer_part_cross_references_manufacturer_parts_Manuf~",
                        column: x => x.ManufacturerPartId,
                        principalSchema: "platform",
                        principalTable: "manufacturer_parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "manufacturer_part_images",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturerPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturer_part_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturer_part_images_manufacturer_parts_ManufacturerPar~",
                        column: x => x.ManufacturerPartId,
                        principalSchema: "platform",
                        principalTable: "manufacturer_parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_listings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturerPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Supplier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SupplierCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    SupplierMsrp = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    WarehouseInventory = table.Column<int>(type: "integer", nullable: true),
                    WarehouseAvailability = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    FreightClass = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsPromotionEligible = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_listings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_listings_manufacturer_parts_ManufacturerPartId",
                        column: x => x.ManufacturerPartId,
                        principalSchema: "platform",
                        principalTable: "manufacturer_parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturer_parts_OrganizationId_Description",
                schema: "platform",
                table: "manufacturer_parts",
                columns: new[] { "OrganizationId", "Description" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturer_parts_SupersededByManufacturerPartId",
                schema: "platform",
                table: "manufacturer_parts",
                column: "SupersededByManufacturerPartId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_ManufacturerPartId",
                schema: "platform",
                table: "inventory_items",
                column: "ManufacturerPartId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_OrganizationId_BinLocation",
                schema: "platform",
                table: "inventory_items",
                columns: new[] { "OrganizationId", "BinLocation" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_OrganizationId_LocationId_ManufacturerPartId",
                schema: "platform",
                table: "inventory_items",
                columns: new[] { "OrganizationId", "LocationId", "ManufacturerPartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturer_part_cross_references_ManufacturerPartId",
                schema: "platform",
                table: "manufacturer_part_cross_references",
                column: "ManufacturerPartId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturer_part_cross_references_OrganizationId_Manufactu~",
                schema: "platform",
                table: "manufacturer_part_cross_references",
                columns: new[] { "OrganizationId", "ManufacturerPartId" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturer_part_cross_references_OrganizationId_Reference~",
                schema: "platform",
                table: "manufacturer_part_cross_references",
                columns: new[] { "OrganizationId", "ReferenceValue" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturer_part_images_ManufacturerPartId",
                schema: "platform",
                table: "manufacturer_part_images",
                column: "ManufacturerPartId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturer_part_images_OrganizationId_ManufacturerPartId_~",
                schema: "platform",
                table: "manufacturer_part_images",
                columns: new[] { "OrganizationId", "ManufacturerPartId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_listings_ManufacturerPartId",
                schema: "platform",
                table: "supplier_listings",
                column: "ManufacturerPartId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_listings_OrganizationId_ManufacturerPartId",
                schema: "platform",
                table: "supplier_listings",
                columns: new[] { "OrganizationId", "ManufacturerPartId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_listings_OrganizationId_Supplier_SupplierSku",
                schema: "platform",
                table: "supplier_listings",
                columns: new[] { "OrganizationId", "Supplier", "SupplierSku" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_manufacturer_parts_manufacturer_parts_SupersededByManufactu~",
                schema: "platform",
                table: "manufacturer_parts",
                column: "SupersededByManufacturerPartId",
                principalSchema: "platform",
                principalTable: "manufacturer_parts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_manufacturer_parts_manufacturer_parts_SupersededByManufactu~",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "manufacturer_part_cross_references",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "manufacturer_part_images",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "supplier_listings",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_manufacturer_parts_OrganizationId_Description",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropIndex(
                name: "IX_manufacturer_parts_SupersededByManufacturerPartId",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Height",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Length",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Map",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Msrp",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Subcategory",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "SupersededByManufacturerPartId",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "platform",
                table: "manufacturer_parts");

            migrationBuilder.DropColumn(
                name: "Width",
                schema: "platform",
                table: "manufacturer_parts");
        }
    }
}
