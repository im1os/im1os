using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessAdministrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                schema: "platform",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                schema: "platform",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "platform",
                table: "organizations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "platform",
                table: "organizations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "platform",
                table: "organizations",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                schema: "platform",
                table: "organizations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Dba",
                schema: "platform",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "platform",
                table: "organizations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "platform",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "platform",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "platform",
                table: "organizations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "platform",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                schema: "platform",
                table: "organizations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                schema: "platform",
                table: "organizations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeFormat",
                schema: "platform",
                table: "organizations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "platform",
                table: "organizations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "platform",
                table: "organizations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryLocationId",
                schema: "platform",
                table: "organization_memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "platform",
                table: "organization_memberships",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultLaborRate",
                schema: "platform",
                table: "locations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTaxRegion",
                schema: "platform",
                table: "locations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoursJson",
                schema: "platform",
                table: "locations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "platform",
                table: "locations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "platform",
                table: "locations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "business_configurations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultLaborRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiagnosticRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EmergencyRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    WeekendRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EnvironmentalFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ShopSuppliesPercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    DefaultTaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    RegionalTaxOverridesJson = table.Column<string>(type: "jsonb", nullable: false),
                    NumberSequencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    NotificationPreferencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DepartmentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConnectorPlaceholdersJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_configurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_configurations_OrganizationId",
                schema: "platform",
                table: "business_configurations",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_configurations",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "DateFormat",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Dba",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Region",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "TaxId",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "TimeFormat",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Website",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PrimaryLocationId",
                schema: "platform",
                table: "organization_memberships");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "platform",
                table: "organization_memberships");

            migrationBuilder.DropColumn(
                name: "DefaultLaborRate",
                schema: "platform",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "DefaultTaxRegion",
                schema: "platform",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "HoursJson",
                schema: "platform",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "platform",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "platform",
                table: "locations");
        }
    }
}
