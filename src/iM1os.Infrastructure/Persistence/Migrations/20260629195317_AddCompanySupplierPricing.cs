using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySupplierPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_supplier_connector_configurations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BaseApiUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DealerAccountNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Username = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApiSecretProtected = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AuthMode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SyncDealerPricingOnSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    DealerPricingScheduleIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    DealerPricingScheduleMaxItems = table.Column<int>(type: "integer", nullable: true),
                    LastDealerPricingScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastConnectionTestAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastConnectionStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastConnectionMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_supplier_connector_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_supplier_connector_configurations_suppliers_Supplie~",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_supplier_prices",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceSupplierProductId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActualDealerCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_supplier_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_supplier_prices_supplier_products_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_supplier_prices_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_supplier_connector_import_runs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanySupplierConnectorConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Source = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_supplier_connector_import_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_supplier_connector_import_runs_company_supplier_con~",
                        column: x => x.CompanySupplierConnectorConfigurationId,
                        principalSchema: "platform",
                        principalTable: "company_supplier_connector_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_connector_configurations_OrganizationId_Su~",
                schema: "platform",
                table: "company_supplier_connector_configurations",
                columns: new[] { "OrganizationId", "SupplierId", "ConnectorKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_connector_configurations_SupplierId",
                schema: "platform",
                table: "company_supplier_connector_configurations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_connector_import_runs_CompanySupplierConne~",
                schema: "platform",
                table: "company_supplier_connector_import_runs",
                column: "CompanySupplierConnectorConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_connector_import_runs_OrganizationId_Compa~",
                schema: "platform",
                table: "company_supplier_connector_import_runs",
                columns: new[] { "OrganizationId", "CompanySupplierConnectorConfigurationId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_connector_import_runs_OrganizationId_Status",
                schema: "platform",
                table: "company_supplier_connector_import_runs",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_prices_OrganizationId_SupplierId_SupplierS~",
                schema: "platform",
                table: "company_supplier_prices",
                columns: new[] { "OrganizationId", "SupplierId", "SupplierSku" });

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_prices_OrganizationId_SupplierProductId",
                schema: "platform",
                table: "company_supplier_prices",
                columns: new[] { "OrganizationId", "SupplierProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_prices_SupplierId",
                schema: "platform",
                table: "company_supplier_prices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_company_supplier_prices_SupplierProductId",
                schema: "platform",
                table: "company_supplier_prices",
                column: "SupplierProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_supplier_connector_import_runs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "company_supplier_prices",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "company_supplier_connector_configurations",
                schema: "platform");
        }
    }
}
