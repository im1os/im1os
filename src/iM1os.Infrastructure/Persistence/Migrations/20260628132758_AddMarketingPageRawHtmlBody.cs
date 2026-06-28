using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingPageRawHtmlBody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawHtmlBody",
                schema: "platform",
                table: "marketing_pages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseRawHtmlBody",
                schema: "platform",
                table: "marketing_pages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawHtmlBody",
                schema: "platform",
                table: "marketing_pages");

            migrationBuilder.DropColumn(
                name: "UseRawHtmlBody",
                schema: "platform",
                table: "marketing_pages");
        }
    }
}
