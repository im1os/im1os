using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalProductTireSearchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TireWidth",
                schema: "platform",
                table: "global_products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TireAspectRatio",
                schema: "platform",
                table: "global_products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TireRimDiameter",
                schema: "platform",
                table: "global_products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TirePosition",
                schema: "platform",
                table: "global_products",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TireConstruction",
                schema: "platform",
                table: "global_products",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TireType",
                schema: "platform",
                table: "global_products",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TireModelLine",
                schema: "platform",
                table: "global_products",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql("""
WITH product_text AS (
    SELECT
        "Id",
        upper(
            coalesce("Brand", '') || ' ' ||
            coalesce("Manufacturer", '') || ' ' ||
            coalesce("Description", '') || ' ' ||
            coalesce("LongDescription", '') || ' ' ||
            coalesce("Category", '') || ' ' ||
            coalesce("SpecificationsJson"::text, '')
        ) AS text
    FROM platform.global_products
),
parsed AS (
    SELECT
        "Id",
        regexp_match(text, '(^|[^0-9])([0-9]{2,3})\s*/\s*([0-9]{2,3})\s*[- ]\s*([0-9]{1,2})($|[^0-9])') AS tire_size,
        regexp_match(text, '\m[A-Z]{1,6}[0-9]{2,4}[A-Z]?\M') AS model_line,
        text
    FROM product_text
    WHERE text ~ '\m(TIRE|TYRE|GEOMAX|MX|MOTOCROSS|OFF[ -]?ROAD|DUAL[ -]?SPORT|STREET|ATV|UTV)\M'
       OR text ~ '(^|[^0-9])([0-9]{2,3})\s*/\s*([0-9]{2,3})\s*[- ]\s*([0-9]{1,2})($|[^0-9])'
)
UPDATE platform.global_products gp
SET
    "TireWidth" = CASE
        WHEN parsed.tire_size IS NOT NULL AND (parsed.tire_size)[2]::int BETWEEN 40 AND 260 THEN (parsed.tire_size)[2]::int
        ELSE gp."TireWidth"
    END,
    "TireAspectRatio" = CASE
        WHEN parsed.tire_size IS NOT NULL AND (parsed.tire_size)[3]::int BETWEEN 20 AND 120 THEN (parsed.tire_size)[3]::int
        ELSE gp."TireAspectRatio"
    END,
    "TireRimDiameter" = CASE
        WHEN parsed.tire_size IS NOT NULL AND (parsed.tire_size)[4]::int BETWEEN 6 AND 30 THEN (parsed.tire_size)[4]::int
        ELSE gp."TireRimDiameter"
    END,
    "TirePosition" = CASE
        WHEN parsed.text ~ '\mFRONT\M|\mFR\M' AND parsed.text ~ '\mREAR\M|\mRR\M' THEN 'front/rear'
        WHEN parsed.text ~ '\mFRONT\M|\mFR\M' THEN 'front'
        WHEN parsed.text ~ '\mREAR\M|\mRR\M' THEN 'rear'
        ELSE gp."TirePosition"
    END,
    "TireConstruction" = CASE
        WHEN parsed.text ~ '\mRADIAL\M|\mZR\M|\mR[0-9]{2}\M' THEN 'radial'
        WHEN parsed.text ~ '\mBIAS\M|\mBIAS-PLY\M|\mTT\M' THEN 'bias'
        ELSE gp."TireConstruction"
    END,
    "TireType" = CASE
        WHEN parsed.text ~ '\mATV\M|\mUTV\M|\mSXS\M|SIDE BY SIDE' THEN 'ATV/UTV'
        WHEN parsed.text ~ '\mSTREET\M|\mCRUISER\M|\mSPORTBIKE\M|\mTOURING\M|\mROAD\M' THEN 'street'
        WHEN parsed.text ~ '\mMX\M|\mMOTOCROSS\M|\mOFF[ -]?ROAD\M|\mGEOMAX\M|\mDUNLOP\M' THEN 'MX/offroad'
        WHEN parsed.text ~ '\mTIRE\M|\mTYRE\M' THEN 'tire'
        ELSE gp."TireType"
    END,
    "TireModelLine" = CASE
        WHEN parsed.model_line IS NOT NULL AND (parsed.model_line)[1] !~ '^[0-9]{2,3}[A-Z]$' THEN (parsed.model_line)[1]
        ELSE gp."TireModelLine"
    END
FROM parsed
WHERE gp."Id" = parsed."Id";
""");

            migrationBuilder.CreateIndex(
                name: "IX_global_products_TireModelLine",
                schema: "platform",
                table: "global_products",
                column: "TireModelLine");

            migrationBuilder.CreateIndex(
                name: "IX_global_products_TireRimDiameter_TireWidth_TireAspectRatio",
                schema: "platform",
                table: "global_products",
                columns: new[] { "TireRimDiameter", "TireWidth", "TireAspectRatio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_global_products_TireModelLine",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropIndex(
                name: "IX_global_products_TireRimDiameter_TireWidth_TireAspectRatio",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "TireAspectRatio",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "TireConstruction",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "TireModelLine",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "TirePosition",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "TireRimDiameter",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "TireType",
                schema: "platform",
                table: "global_products");

            migrationBuilder.DropColumn(
                name: "TireWidth",
                schema: "platform",
                table: "global_products");
        }
    }
}
