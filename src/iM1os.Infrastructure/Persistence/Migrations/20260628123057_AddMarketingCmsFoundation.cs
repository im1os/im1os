using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingCmsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marketing_leads",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Company = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketing_leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketing_pages",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    NavigationLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenGraphTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    OpenGraphDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenGraphImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CanonicalUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketing_pages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketing_content_blocks",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketingPageId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Eyebrow = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Heading = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PrimaryActionLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PrimaryActionUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SecondaryActionLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SecondaryActionUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ItemsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketing_content_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_marketing_content_blocks_marketing_pages_MarketingPageId",
                        column: x => x.MarketingPageId,
                        principalSchema: "platform",
                        principalTable: "marketing_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marketing_content_blocks_MarketingPageId_SortOrder",
                schema: "platform",
                table: "marketing_content_blocks",
                columns: new[] { "MarketingPageId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_marketing_leads_CreatedAtUtc",
                schema: "platform",
                table: "marketing_leads",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_marketing_pages_IsPublished_SortOrder",
                schema: "platform",
                table: "marketing_pages",
                columns: new[] { "IsPublished", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_marketing_pages_Slug",
                schema: "platform",
                table: "marketing_pages",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketing_content_blocks",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "marketing_leads",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "marketing_pages",
                schema: "platform");
        }
    }
}
