using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalBrandAliasesAndNormalizationSafeguards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_canonical_item_sources_SourceTable_SourceKey",
                schema: "platform",
                table: "canonical_item_sources");

            migrationBuilder.DropIndex(
                name: "IX_canonical_fitments_CanonicalItemId_Year_Make_Model_Submodel~",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.DropIndex(
                name: "IX_canonical_fitments_Year_Make_Model",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.AddColumn<string>(
                name: "EngineKey",
                schema: "platform",
                table: "canonical_fitments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MakeKey",
                schema: "platform",
                table: "canonical_fitments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModelKey",
                schema: "platform",
                table: "canonical_fitments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmodelKey",
                schema: "platform",
                table: "canonical_fitments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "canonical_brand_aliases",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedBrand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CanonicalBrand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_brand_aliases", x => x.Id);
                });

            migrationBuilder.Sql("""
                UPDATE platform.canonical_fitments
                SET
                    "MakeKey" = upper(regexp_replace(btrim("Make"), '[[:space:]]+', ' ', 'g')),
                    "ModelKey" = upper(regexp_replace(btrim("Model"), '[[:space:]]+', ' ', 'g')),
                    "SubmodelKey" = coalesce(upper(regexp_replace(btrim("Submodel"), '[[:space:]]+', ' ', 'g')), ''),
                    "EngineKey" = coalesce(upper(regexp_replace(btrim("Engine"), '[[:space:]]+', ' ', 'g')), '');
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT
                        ctid,
                        row_number() OVER (
                            PARTITION BY "SourceTable", "SourceKey"
                            ORDER BY "CreatedAtUtc", "Id"
                        ) AS row_number
                    FROM platform.canonical_item_sources
                )
                DELETE FROM platform.canonical_item_sources source
                USING ranked
                WHERE source.ctid = ranked.ctid
                    AND ranked.row_number > 1;
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT
                        ctid,
                        row_number() OVER (
                            PARTITION BY "CanonicalItemId", "IdentifierType", "NormalizedValue", "SupplierProductId"
                            ORDER BY "CreatedAtUtc", "Id"
                        ) AS row_number
                    FROM platform.canonical_item_identifiers
                )
                DELETE FROM platform.canonical_item_identifiers identifier
                USING ranked
                WHERE identifier.ctid = ranked.ctid
                    AND ranked.row_number > 1;
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT
                        ctid,
                        row_number() OVER (
                            PARTITION BY "CanonicalItemId", "Year", "MakeKey", "ModelKey", "SubmodelKey", "EngineKey"
                            ORDER BY "CreatedAtUtc", "Id"
                        ) AS row_number
                    FROM platform.canonical_fitments
                )
                DELETE FROM platform.canonical_fitments fitment
                USING ranked
                WHERE fitment.ctid = ranked.ctid
                    AND ranked.row_number > 1;
                """);

            migrationBuilder.Sql("""
                INSERT INTO platform.canonical_brand_aliases
                    ("Id", "Brand", "NormalizedBrand", "CanonicalBrand", "Notes", "IsActive", "CreatedAtUtc")
                VALUES
                    ('6d28d9e5-9c26-4a1e-a4a0-f5f4ddf10f01', 'K&S', 'KS', 'K&S TECHNOLOGIES', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00'),
                    ('0a5033a5-5d10-4f35-8bf1-fecdf6fba102', 'K&S TECHNOLOGIES', 'KSTECHNOLOGIES', 'K&S TECHNOLOGIES', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00'),
                    ('3b2c7172-f048-4b30-9057-5d6e3bce8403', 'D-COR', 'DCOR', 'D''COR VISUALS', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00'),
                    ('fe5be0f8-52dd-4eca-879f-b6c36e38c804', 'D''COR VISUALS', 'DCORVISUALS', 'D''COR VISUALS', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00'),
                    ('1a0b1b8a-377c-4abd-a7fc-376d1250f905', 'NGK', 'NGK', 'NGK SPARK PLUGS', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00'),
                    ('7b560a85-1cd7-4511-a75f-544d50780606', 'NGK SPARK PLUGS', 'NGKSPARKPLUGS', 'NGK SPARK PLUGS', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00'),
                    ('309aeaa4-f8d5-48d0-88ec-78caa2c9f807', 'ALL BALLS', 'ALLBALLS', 'ALL BALLS', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00'),
                    ('f4d3ad15-fb6a-4692-bb87-0d7ec8cf9708', 'ALL BALLS RACING', 'ALLBALLSRACING', 'ALL BALLS', 'Initial catalog normalization alias.', true, '2026-07-05 00:00:00+00');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_sources_SourceTable_SourceKey",
                schema: "platform",
                table: "canonical_item_sources",
                columns: new[] { "SourceTable", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_identifiers_CanonicalItemId_IdentifierType_N~",
                schema: "platform",
                table: "canonical_item_identifiers",
                columns: new[] { "CanonicalItemId", "IdentifierType", "NormalizedValue", "SupplierProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_CanonicalItemId_Year_MakeKey_ModelKey_Su~",
                schema: "platform",
                table: "canonical_fitments",
                columns: new[] { "CanonicalItemId", "Year", "MakeKey", "ModelKey", "SubmodelKey", "EngineKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_Year_MakeKey_ModelKey",
                schema: "platform",
                table: "canonical_fitments",
                columns: new[] { "Year", "MakeKey", "ModelKey" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_brand_aliases_CanonicalBrand_IsActive",
                schema: "platform",
                table: "canonical_brand_aliases",
                columns: new[] { "CanonicalBrand", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_brand_aliases_NormalizedBrand",
                schema: "platform",
                table: "canonical_brand_aliases",
                column: "NormalizedBrand",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canonical_brand_aliases",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_canonical_item_sources_SourceTable_SourceKey",
                schema: "platform",
                table: "canonical_item_sources");

            migrationBuilder.DropIndex(
                name: "IX_canonical_item_identifiers_CanonicalItemId_IdentifierType_N~",
                schema: "platform",
                table: "canonical_item_identifiers");

            migrationBuilder.DropIndex(
                name: "IX_canonical_fitments_CanonicalItemId_Year_MakeKey_ModelKey_Su~",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.DropIndex(
                name: "IX_canonical_fitments_Year_MakeKey_ModelKey",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.DropColumn(
                name: "EngineKey",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.DropColumn(
                name: "MakeKey",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.DropColumn(
                name: "ModelKey",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.DropColumn(
                name: "SubmodelKey",
                schema: "platform",
                table: "canonical_fitments");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_item_sources_SourceTable_SourceKey",
                schema: "platform",
                table: "canonical_item_sources",
                columns: new[] { "SourceTable", "SourceKey" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_CanonicalItemId_Year_Make_Model_Submodel~",
                schema: "platform",
                table: "canonical_fitments",
                columns: new[] { "CanonicalItemId", "Year", "Make", "Model", "Submodel", "Engine" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canonical_fitments_Year_Make_Model",
                schema: "platform",
                table: "canonical_fitments",
                columns: new[] { "Year", "Make", "Model" });
        }
    }
}
