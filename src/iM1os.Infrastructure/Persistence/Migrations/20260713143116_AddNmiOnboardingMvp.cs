using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNmiOnboardingMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PublicTokenizationKey",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationCreateIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationSubmitIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialProvisioningStatus",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CredentialsProvisionedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LegalConsentCompletedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalConsentUrlProtected",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCredentialIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderApplicationCreatedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderApplicationSubmittedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderApprovedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderStatusRefreshedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenizationCredentialIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumberProtected",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankRoutingNumberProtected",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessDescription",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardPresentPercentage",
                schema: "platform",
                table: "merchant_accounts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EcommercePercentage",
                schema: "platform",
                table: "merchant_accounts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HighTicket",
                schema: "platform",
                table: "merchant_accounts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KeyEnteredPercentage",
                schema: "platform",
                table: "merchant_accounts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MotoPercentage",
                schema: "platform",
                table: "merchant_accounts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerDateOfBirthProtected",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OwnerOwnershipPercentage",
                schema: "platform",
                table: "merchant_accounts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerSsnLastFour",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerSsnProtected",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerTitle",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentifierProtected",
                schema: "platform",
                table: "merchant_accounts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsInBusiness",
                schema: "platform",
                table: "merchant_accounts",
                type: "integer",
                nullable: true);

            // Values written by the pre-MVP implementation cannot be proven to have been
            // protected at rest. Remove them and require safe reprovisioning instead of
            // treating a legacy merchant as active with unverifiable credentials.
            migrationBuilder.Sql(
                """
                UPDATE platform.merchant_provider_relationships
                SET "CredentialProvisioningStatus" = CASE
                        WHEN "GatewayPasswordProtected" IS NOT NULL
                          OR "PaymentApiKeyProtected" IS NOT NULL
                          OR "QueryApiKeyProtected" IS NOT NULL
                          OR "PublicTokenizationKey" IS NOT NULL
                        THEN 'ReconciliationRequired'
                        ELSE 'NotStarted'
                    END,
                    "LastProviderError" = CASE
                        WHEN "GatewayPasswordProtected" IS NOT NULL
                          OR "PaymentApiKeyProtected" IS NOT NULL
                          OR "QueryApiKeyProtected" IS NOT NULL
                          OR "PublicTokenizationKey" IS NOT NULL
                        THEN 'Legacy credentials were removed and must be securely reprovisioned.'
                        ELSE "LastProviderError"
                    END,
                    "GatewayPasswordProtected" = NULL,
                    "PaymentApiKeyProtected" = NULL,
                    "QueryApiKeyProtected" = NULL,
                    "PublicTokenizationKey" = NULL,
                    "CapabilitiesJson" = NULL,
                    "CredentialMetadataJson" = NULL,
                    "SupportNotes" = NULL;

                UPDATE platform.merchant_accounts AS account
                SET "Status" = 'CredentialProvisioning',
                    "UnderwritingStatus" = 'Approved',
                    "PaymentsEnabled" = FALSE,
                    "UpdatedAtUtc" = NOW()
                FROM platform.merchant_provider_relationships AS relationship
                WHERE relationship."MerchantAccountId" = account."Id"
                  AND relationship."CredentialProvisioningStatus" = 'ReconciliationRequired'
                  AND account."Status" = 'Active';

                UPDATE platform.merchant_provider_relationships AS relationship
                SET "Status" = 'CredentialProvisioning'
                FROM platform.merchant_accounts AS account
                WHERE relationship."MerchantAccountId" = account."Id"
                  AND relationship."CredentialProvisioningStatus" = 'ReconciliationRequired'
                  AND account."Status" = 'CredentialProvisioning';

                UPDATE platform.merchant_accounts
                SET "TaxIdentifierLastFour" = RIGHT("Ein", 4),
                    "Ein" = '****' || RIGHT("Ein", 4)
                WHERE "Ein" IS NOT NULL
                  AND LENGTH("Ein") >= 4;

                UPDATE platform.payment_transactions
                SET "RawResponseJson" = NULL
                WHERE "RawResponseJson" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationCreateIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "ApplicationSubmitIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "CredentialProvisioningStatus",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "CredentialsProvisionedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "LegalConsentCompletedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "LegalConsentUrlProtected",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "PaymentCredentialIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "ProviderApplicationCreatedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "ProviderApplicationSubmittedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "ProviderApprovedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "ProviderStatusRefreshedAtUtc",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "TokenizationCredentialIdempotencyKey",
                schema: "platform",
                table: "merchant_provider_relationships");

            migrationBuilder.DropColumn(
                name: "BankAccountNumberProtected",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "BankRoutingNumberProtected",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "BusinessDescription",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "CardPresentPercentage",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "EcommercePercentage",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "HighTicket",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "KeyEnteredPercentage",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "MotoPercentage",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "OwnerDateOfBirthProtected",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "OwnerOwnershipPercentage",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "OwnerSsnLastFour",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "OwnerSsnProtected",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "OwnerTitle",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "TaxIdentifierProtected",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.DropColumn(
                name: "YearsInBusiness",
                schema: "platform",
                table: "merchant_accounts");

            migrationBuilder.AlterColumn<string>(
                name: "PublicTokenizationKey",
                schema: "platform",
                table: "merchant_provider_relationships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
