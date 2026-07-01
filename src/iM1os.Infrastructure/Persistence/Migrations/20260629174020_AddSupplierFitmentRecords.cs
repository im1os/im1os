using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierFitmentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_fitment_records",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    GlobalProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    GlobalVehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleFitmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceSupplierProductId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SupplierPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SupplierSku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceFitmentItemId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceFitmentPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MfgPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Make = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Submodel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Engine = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolutionStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_fitment_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_fitment_records_global_products_GlobalProductId",
                        column: x => x.GlobalProductId,
                        principalSchema: "platform",
                        principalTable: "global_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_supplier_fitment_records_global_vehicles_GlobalVehicleId",
                        column: x => x.GlobalVehicleId,
                        principalSchema: "platform",
                        principalTable: "global_vehicles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_supplier_fitment_records_supplier_products_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_supplier_fitment_records_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_fitment_records_vehicle_fitments_VehicleFitmentId",
                        column: x => x.VehicleFitmentId,
                        principalSchema: "platform",
                        principalTable: "vehicle_fitments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_GlobalProductId",
                schema: "platform",
                table: "supplier_fitment_records",
                column: "GlobalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_GlobalVehicleId",
                schema: "platform",
                table: "supplier_fitment_records",
                column: "GlobalVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_ResolutionStatus",
                schema: "platform",
                table: "supplier_fitment_records",
                column: "ResolutionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_SupplierId_SourceFitmentItemId_Yea~",
                schema: "platform",
                table: "supplier_fitment_records",
                columns: new[] { "SupplierId", "SourceFitmentItemId", "Year", "Make", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_SupplierId_SourceSupplierProductId",
                schema: "platform",
                table: "supplier_fitment_records",
                columns: new[] { "SupplierId", "SourceSupplierProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_SupplierId_SupplierSku",
                schema: "platform",
                table: "supplier_fitment_records",
                columns: new[] { "SupplierId", "SupplierSku" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_SupplierProductId",
                schema: "platform",
                table: "supplier_fitment_records",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_VehicleFitmentId",
                schema: "platform",
                table: "supplier_fitment_records",
                column: "VehicleFitmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_fitment_records",
                schema: "platform");
        }
    }
}
