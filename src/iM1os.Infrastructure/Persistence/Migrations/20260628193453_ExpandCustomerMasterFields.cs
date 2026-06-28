using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCustomerMasterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowEmailMarketing",
                schema: "platform",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowPhoneCalls",
                schema: "platform",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSmsMarketing",
                schema: "platform",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Anniversary",
                schema: "platform",
                table: "customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                schema: "platform",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                schema: "platform",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNumber",
                schema: "platform",
                table: "customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CustomerSince",
                schema: "platform",
                table: "customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "platform",
                table: "customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomePhone",
                schema: "platform",
                table: "customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPurchaseAtUtc",
                schema: "platform",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LifetimeSales",
                schema: "platform",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                schema: "platform",
                table: "customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobilePhone",
                schema: "platform",
                table: "customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                schema: "platform",
                table: "customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                schema: "platform",
                table: "customers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryEmail",
                schema: "platform",
                table: "customers",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StoreCredit",
                schema: "platform",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SummaryNotes",
                schema: "platform",
                table: "customers",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TaxExempt",
                schema: "platform",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxExemptNumber",
                schema: "platform",
                table: "customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkPhone",
                schema: "platform",
                table: "customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (PARTITION BY "OrganizationId" ORDER BY "CreatedAtUtc", "Id") AS row_number
                    FROM platform.customers
                    WHERE "CustomerNumber" IS NULL
                )
                UPDATE platform.customers AS customer
                SET
                    "CustomerNumber" = 'CUS-' || LPAD(numbered.row_number::text, 6, '0'),
                    "CustomerSince" = COALESCE(customer."CustomerSince", customer."CreatedAtUtc"::date),
                    "MobilePhone" = COALESCE(customer."MobilePhone", customer."Phone")
                FROM numbered
                WHERE customer."Id" = numbered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_customers_OrganizationId_CustomerNumber",
                schema: "platform",
                table: "customers",
                columns: new[] { "OrganizationId", "CustomerNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customers_OrganizationId_CustomerNumber",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AllowEmailMarketing",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AllowPhoneCalls",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AllowSmsMarketing",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Anniversary",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CustomerNumber",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CustomerSince",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "HomePhone",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "LastPurchaseAtUtc",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "LifetimeSales",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "MobilePhone",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Nickname",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "SecondaryEmail",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "StoreCredit",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "SummaryNotes",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "TaxExempt",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "TaxExemptNumber",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "WorkPhone",
                schema: "platform",
                table: "customers");
        }
    }
}
