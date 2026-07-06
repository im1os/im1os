using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Environment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    GatewayTransactionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ResponseCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ResponseText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrderId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CardBrand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CardLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    RequestCorrelationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RawResponseJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "platform",
                        principalTable: "locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_LocationId",
                schema: "platform",
                table: "payment_transactions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_OrganizationId_CreatedAtUtc",
                schema: "platform",
                table: "payment_transactions",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_OrganizationId_GatewayTransactionId",
                schema: "platform",
                table: "payment_transactions",
                columns: new[] { "OrganizationId", "GatewayTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_OrganizationId_Status",
                schema: "platform",
                table: "payment_transactions",
                columns: new[] { "OrganizationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "platform");
        }
    }
}
