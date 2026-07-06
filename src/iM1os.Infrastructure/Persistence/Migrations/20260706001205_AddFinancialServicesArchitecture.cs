using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialServicesArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_export_batches",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExportReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_export_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "banking_connections",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AccountDescriptor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ProviderToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banking_connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_wallets",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreferredPaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_wallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_wallets_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_ledger_entries",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_ledger_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_provider_connections",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ConfigurationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_provider_connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financing_applications",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financing_applications_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "merchant_accounts",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UnderwritingStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LegalBusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TaxIdentifierLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ProcessingProfile = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SettlementSchedule = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PrimaryProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_terminals",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderTerminalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AssignedRegister = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AssignedEmployeeId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_terminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_terminals_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "platform",
                        principalTable: "locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "subscription_agreements",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BillingCadence = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscription_agreements_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "wallet_payment_methods",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MethodType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisplayBrand = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorizedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_payment_methods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wallet_payment_methods_customer_wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "platform",
                        principalTable: "customer_wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merchant_provider_relationships",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderMerchantId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_provider_relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_provider_relationships_merchant_accounts_MerchantA~",
                        column: x => x.MerchantAccountId,
                        principalSchema: "platform",
                        principalTable: "merchant_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_export_batches_OrganizationId_PeriodStartUtc_Per~",
                schema: "platform",
                table: "accounting_export_batches",
                columns: new[] { "OrganizationId", "PeriodStartUtc", "PeriodEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_banking_connections_OrganizationId_ProviderCode",
                schema: "platform",
                table: "banking_connections",
                columns: new[] { "OrganizationId", "ProviderCode" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_wallets_CustomerId",
                schema: "platform",
                table: "customer_wallets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_wallets_OrganizationId_CustomerId",
                schema: "platform",
                table: "customer_wallets",
                columns: new[] { "OrganizationId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_ledger_entries_OrganizationId_EntryType",
                schema: "platform",
                table: "financial_ledger_entries",
                columns: new[] { "OrganizationId", "EntryType" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_ledger_entries_OrganizationId_OccurredAtUtc",
                schema: "platform",
                table: "financial_ledger_entries",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_ledger_entries_OrganizationId_SourceType_SourceId",
                schema: "platform",
                table: "financial_ledger_entries",
                columns: new[] { "OrganizationId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_provider_connections_OrganizationId_ProviderCode_~",
                schema: "platform",
                table: "financial_provider_connections",
                columns: new[] { "OrganizationId", "ProviderCode", "ProviderType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financing_applications_CustomerId",
                schema: "platform",
                table: "financing_applications",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_financing_applications_OrganizationId_CustomerId",
                schema: "platform",
                table: "financing_applications",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_accounts_OrganizationId",
                schema: "platform",
                table: "merchant_accounts",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchant_provider_relationships_MerchantAccountId",
                schema: "platform",
                table: "merchant_provider_relationships",
                column: "MerchantAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_provider_relationships_OrganizationId_ProviderCode",
                schema: "platform",
                table: "merchant_provider_relationships",
                columns: new[] { "OrganizationId", "ProviderCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_terminals_LocationId",
                schema: "platform",
                table: "payment_terminals",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_terminals_OrganizationId_LocationId",
                schema: "platform",
                table: "payment_terminals",
                columns: new[] { "OrganizationId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_terminals_OrganizationId_ProviderCode_ProviderTermi~",
                schema: "platform",
                table: "payment_terminals",
                columns: new[] { "OrganizationId", "ProviderCode", "ProviderTerminalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_agreements_CustomerId",
                schema: "platform",
                table: "subscription_agreements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_agreements_OrganizationId_CustomerId",
                schema: "platform",
                table: "subscription_agreements",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_payment_methods_OrganizationId_ProviderCode_Provider~",
                schema: "platform",
                table: "wallet_payment_methods",
                columns: new[] { "OrganizationId", "ProviderCode", "ProviderToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_payment_methods_OrganizationId_WalletId",
                schema: "platform",
                table: "wallet_payment_methods",
                columns: new[] { "OrganizationId", "WalletId" });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_payment_methods_WalletId",
                schema: "platform",
                table: "wallet_payment_methods",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_export_batches",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "banking_connections",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "financial_ledger_entries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "financial_provider_connections",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "financing_applications",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "merchant_provider_relationships",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "payment_terminals",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "subscription_agreements",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "wallet_payment_methods",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "merchant_accounts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "customer_wallets",
                schema: "platform");
        }
    }
}
