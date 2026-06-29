using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerUnitDetailsAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "platform",
                table: "customer_vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MileageIn",
                schema: "platform",
                table: "customer_vehicles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MileageOut",
                schema: "platform",
                table: "customer_vehicles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "platform",
                table: "customer_vehicles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagPlate",
                schema: "platform",
                table: "customer_vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "platform",
                table: "customer_vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Off-Road");

            migrationBuilder.CreateTable(
                name: "customer_vehicle_attachments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerVehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachmentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
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
                    table.PrimaryKey("PK_customer_vehicle_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_vehicle_attachments_customer_vehicles_CustomerVehi~",
                        column: x => x.CustomerVehicleId,
                        principalSchema: "platform",
                        principalTable: "customer_vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_vehicle_attachments_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_vehicle_attachments_CustomerId",
                schema: "platform",
                table: "customer_vehicle_attachments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_vehicle_attachments_CustomerVehicleId",
                schema: "platform",
                table: "customer_vehicle_attachments",
                column: "CustomerVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_vehicle_attachments_OrganizationId_CustomerId",
                schema: "platform",
                table: "customer_vehicle_attachments",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_vehicle_attachments_OrganizationId_CustomerVehicle~",
                schema: "platform",
                table: "customer_vehicle_attachments",
                columns: new[] { "OrganizationId", "CustomerVehicleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_vehicle_attachments",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "Color",
                schema: "platform",
                table: "customer_vehicles");

            migrationBuilder.DropColumn(
                name: "MileageIn",
                schema: "platform",
                table: "customer_vehicles");

            migrationBuilder.DropColumn(
                name: "MileageOut",
                schema: "platform",
                table: "customer_vehicles");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "platform",
                table: "customer_vehicles");

            migrationBuilder.DropColumn(
                name: "TagPlate",
                schema: "platform",
                table: "customer_vehicles");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "platform",
                table: "customer_vehicles");
        }
    }
}
