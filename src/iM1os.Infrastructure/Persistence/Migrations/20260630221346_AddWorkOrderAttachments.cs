using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_order_attachments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerVehicleId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_work_order_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_attachments_customer_vehicles_CustomerVehicleId",
                        column: x => x.CustomerVehicleId,
                        principalSchema: "platform",
                        principalTable: "customer_vehicles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_work_order_attachments_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "platform",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_attachments_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "platform",
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_attachments_CustomerId",
                schema: "platform",
                table: "work_order_attachments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_attachments_CustomerVehicleId",
                schema: "platform",
                table: "work_order_attachments",
                column: "CustomerVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_attachments_OrganizationId_CustomerId",
                schema: "platform",
                table: "work_order_attachments",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_attachments_OrganizationId_CustomerVehicleId",
                schema: "platform",
                table: "work_order_attachments",
                columns: new[] { "OrganizationId", "CustomerVehicleId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_attachments_OrganizationId_WorkOrderId",
                schema: "platform",
                table: "work_order_attachments",
                columns: new[] { "OrganizationId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_attachments_WorkOrderId",
                schema: "platform",
                table: "work_order_attachments",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order_attachments",
                schema: "platform");
        }
    }
}
