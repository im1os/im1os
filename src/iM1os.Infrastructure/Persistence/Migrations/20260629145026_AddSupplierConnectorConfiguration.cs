using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierConnectorConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_connector_configurations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    ImportMasterFileOnSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    MasterFileImportMode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
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
                    table.PrimaryKey("PK_supplier_connector_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_connector_configurations_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_connector_import_runs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierConnectorConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RequestedByPlatformUserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_supplier_connector_import_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_connector_import_runs_supplier_connector_configura~",
                        column: x => x.SupplierConnectorConfigurationId,
                        principalSchema: "platform",
                        principalTable: "supplier_connector_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_connector_configurations_SupplierId_ConnectorKey",
                schema: "platform",
                table: "supplier_connector_configurations",
                columns: new[] { "SupplierId", "ConnectorKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_connector_import_runs_Status",
                schema: "platform",
                table: "supplier_connector_import_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_connector_import_runs_SupplierConnectorConfigurati~",
                schema: "platform",
                table: "supplier_connector_import_runs",
                columns: new[] { "SupplierConnectorConfigurationId", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_connector_import_runs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "supplier_connector_configurations",
                schema: "platform");
        }
    }
}
