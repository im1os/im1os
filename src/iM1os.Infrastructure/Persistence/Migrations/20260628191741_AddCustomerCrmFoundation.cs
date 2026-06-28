using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCrmFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerType",
                schema: "platform",
                table: "customers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "platform",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStage",
                schema: "platform",
                table: "customers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredContactMethod",
                schema: "platform",
                table: "customers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "platform",
                table: "customers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "platform",
                table: "customers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.CreateTable(
                name: "customer_addresses",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Region = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsBilling = table.Column<bool>(type: "boolean", nullable: false),
                    IsShipping = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_addresses_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_custom_fields",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FieldLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FieldValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_custom_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_custom_fields_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_documents",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_documents_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_external_links",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalCustomerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_external_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_external_links_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_notes",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NoteType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_notes_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_phone_numbers",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CanText = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_phone_numbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_phone_numbers_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_tags",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_tags_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customers_OrganizationId_LifecycleStage",
                schema: "platform",
                table: "customers",
                columns: new[] { "OrganizationId", "LifecycleStage" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_OrganizationId_Status",
                schema: "platform",
                table: "customers",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_CustomerId",
                schema: "platform",
                table: "customer_addresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_OrganizationId_CustomerId",
                schema: "platform",
                table: "customer_addresses",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_OrganizationId_PostalCode",
                schema: "platform",
                table: "customer_addresses",
                columns: new[] { "OrganizationId", "PostalCode" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_custom_fields_CustomerId",
                schema: "platform",
                table: "customer_custom_fields",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_custom_fields_OrganizationId_CustomerId_FieldKey",
                schema: "platform",
                table: "customer_custom_fields",
                columns: new[] { "OrganizationId", "CustomerId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_documents_CustomerId",
                schema: "platform",
                table: "customer_documents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_documents_OrganizationId_CustomerId",
                schema: "platform",
                table: "customer_documents",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_documents_OrganizationId_DocumentType",
                schema: "platform",
                table: "customer_documents",
                columns: new[] { "OrganizationId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_external_links_CustomerId",
                schema: "platform",
                table: "customer_external_links",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_external_links_OrganizationId_CustomerId",
                schema: "platform",
                table: "customer_external_links",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_external_links_OrganizationId_Provider_ExternalCus~",
                schema: "platform",
                table: "customer_external_links",
                columns: new[] { "OrganizationId", "Provider", "ExternalCustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_notes_CustomerId",
                schema: "platform",
                table: "customer_notes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_notes_OrganizationId_CustomerId_OccurredAtUtc",
                schema: "platform",
                table: "customer_notes",
                columns: new[] { "OrganizationId", "CustomerId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_notes_OrganizationId_NoteType",
                schema: "platform",
                table: "customer_notes",
                columns: new[] { "OrganizationId", "NoteType" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_phone_numbers_CustomerId",
                schema: "platform",
                table: "customer_phone_numbers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_phone_numbers_OrganizationId_CustomerId",
                schema: "platform",
                table: "customer_phone_numbers",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_phone_numbers_OrganizationId_PhoneNumber",
                schema: "platform",
                table: "customer_phone_numbers",
                columns: new[] { "OrganizationId", "PhoneNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_tags_CustomerId",
                schema: "platform",
                table: "customer_tags",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_tags_OrganizationId_CustomerId",
                schema: "platform",
                table: "customer_tags",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_tags_OrganizationId_Tag",
                schema: "platform",
                table: "customer_tags",
                columns: new[] { "OrganizationId", "Tag" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_addresses",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "customer_custom_fields",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "customer_documents",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "customer_external_links",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "customer_notes",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "customer_phone_numbers",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "customer_tags",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_customers_OrganizationId_LifecycleStage",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_OrganizationId_Status",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "LifecycleStage",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "PreferredContactMethod",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "platform",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "platform",
                table: "customers");
        }
    }
}
