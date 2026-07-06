using System;
using iM1os.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260706125000_AddMerchantAccountLifecycle")]
    public partial class AddMerchantAccountLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastProviderError",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportNotes",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayUsername",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPasswordProtected",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentApiKeyProtected",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueryApiKeyProtected",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicTokenizationKey",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialMetadataJson",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(name: "Dba", schema: "platform", table: "merchant_accounts", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Ein", schema: "platform", table: "merchant_accounts", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "BusinessType", schema: "platform", table: "merchant_accounts", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PhysicalAddressLine1", schema: "platform", table: "merchant_accounts", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PhysicalAddressLine2", schema: "platform", table: "merchant_accounts", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PhysicalCity", schema: "platform", table: "merchant_accounts", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PhysicalRegion", schema: "platform", table: "merchant_accounts", type: "character varying(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PhysicalPostalCode", schema: "platform", table: "merchant_accounts", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PhysicalCountry", schema: "platform", table: "merchant_accounts", type: "character varying(2)", maxLength: 2, nullable: true);
            migrationBuilder.AddColumn<string>(name: "MailingAddressLine1", schema: "platform", table: "merchant_accounts", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "MailingAddressLine2", schema: "platform", table: "merchant_accounts", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "MailingCity", schema: "platform", table: "merchant_accounts", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "MailingRegion", schema: "platform", table: "merchant_accounts", type: "character varying(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<string>(name: "MailingPostalCode", schema: "platform", table: "merchant_accounts", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "MailingCountry", schema: "platform", table: "merchant_accounts", type: "character varying(2)", maxLength: 2, nullable: true);
            migrationBuilder.AddColumn<string>(name: "OwnerName", schema: "platform", table: "merchant_accounts", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "OwnerEmail", schema: "platform", table: "merchant_accounts", type: "character varying(320)", maxLength: 320, nullable: true);
            migrationBuilder.AddColumn<string>(name: "OwnerPhone", schema: "platform", table: "merchant_accounts", type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(name: "BankName", schema: "platform", table: "merchant_accounts", type: "character varying(160)", maxLength: 160, nullable: true);
            migrationBuilder.AddColumn<string>(name: "BankRoutingLastFour", schema: "platform", table: "merchant_accounts", type: "character varying(4)", maxLength: 4, nullable: true);
            migrationBuilder.AddColumn<string>(name: "BankAccountLastFour", schema: "platform", table: "merchant_accounts", type: "character varying(4)", maxLength: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ExpectedMonthlyVolume", schema: "platform", table: "merchant_accounts", type: "numeric(12,2)", precision: 12, scale: 2, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "AverageTicket", schema: "platform", table: "merchant_accounts", type: "numeric(12,2)", precision: 12, scale: 2, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Website", schema: "platform", table: "merchant_accounts", type: "character varying(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Mcc", schema: "platform", table: "merchant_accounts", type: "character varying(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "PaymentsEnabled", schema: "platform", table: "merchant_accounts", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "SubmittedAtUtc", schema: "platform", table: "merchant_accounts", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "ApprovedAtUtc", schema: "platform", table: "merchant_accounts", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "ActivatedAtUtc", schema: "platform", table: "merchant_accounts", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "RejectedAtUtc", schema: "platform", table: "merchant_accounts", type: "timestamp with time zone", nullable: true);

            migrationBuilder.CreateTable(
                name: "merchant_account_status_history",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_account_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_account_status_history_merchant_accounts_MerchantAc~",
                        column: x => x.MerchantAccountId,
                        principalSchema: "platform",
                        principalTable: "merchant_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_account_status_history_MerchantAccountId",
                schema: "platform",
                table: "merchant_account_status_history",
                column: "MerchantAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_account_status_history_OrganizationId_MerchantAcc~",
                schema: "platform",
                table: "merchant_account_status_history",
                columns: new[] { "OrganizationId", "MerchantAccountId", "CreatedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merchant_account_status_history",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "LastProviderError",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "SupportNotes",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(name: "GatewayUsername", schema: "platform", table: "merchant_provider_relationships");
            migrationBuilder.DropColumn(name: "GatewayPasswordProtected", schema: "platform", table: "merchant_provider_relationships");
            migrationBuilder.DropColumn(name: "PaymentApiKeyProtected", schema: "platform", table: "merchant_provider_relationships");
            migrationBuilder.DropColumn(name: "QueryApiKeyProtected", schema: "platform", table: "merchant_provider_relationships");
            migrationBuilder.DropColumn(name: "PublicTokenizationKey", schema: "platform", table: "merchant_provider_relationships");
            migrationBuilder.DropColumn(name: "CredentialMetadataJson", schema: "platform", table: "merchant_provider_relationships");
            migrationBuilder.DropColumn(name: "Dba", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "Ein", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "BusinessType", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "PhysicalAddressLine1", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "PhysicalAddressLine2", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "PhysicalCity", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "PhysicalRegion", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "PhysicalPostalCode", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "PhysicalCountry", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "MailingAddressLine1", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "MailingAddressLine2", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "MailingCity", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "MailingRegion", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "MailingPostalCode", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "MailingCountry", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "OwnerName", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "OwnerEmail", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "OwnerPhone", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "BankName", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "BankRoutingLastFour", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "BankAccountLastFour", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "ExpectedMonthlyVolume", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "AverageTicket", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "Website", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "Mcc", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "PaymentsEnabled", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "SubmittedAtUtc", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "ApprovedAtUtc", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "ActivatedAtUtc", schema: "platform", table: "merchant_accounts");
            migrationBuilder.DropColumn(name: "RejectedAtUtc", schema: "platform", table: "merchant_accounts");
        }
    }
}
