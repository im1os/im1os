using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncSupplierWorkerFixModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiagnosisFindings",
                schema: "platform",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IntakeDate",
                schema: "platform",
                table: "work_orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartsAndSuppliesNotes",
                schema: "platform",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PromiseDate",
                schema: "platform",
                table: "work_orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepairOrderNumber",
                schema: "platform",
                table: "work_orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceNotes",
                schema: "platform",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositTerms",
                schema: "platform",
                table: "estimates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountTotal",
                schema: "platform",
                table: "estimates",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FeesTotal",
                schema: "platform",
                table: "estimates",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                schema: "platform",
                table: "estimates",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                schema: "platform",
                table: "estimates",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "estimate_line_items",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManufacturerPartId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Sku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeclined = table.Column<bool>(type: "boolean", nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estimate_line_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_estimate_line_items_estimates_EstimateId",
                        column: x => x.EstimateId,
                        principalSchema: "platform",
                        principalTable: "estimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_estimate_line_items_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "platform",
                        principalTable: "inventory_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_estimate_line_items_labor_operations_LaborOperationId",
                        column: x => x.LaborOperationId,
                        principalSchema: "platform",
                        principalTable: "labor_operations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_estimate_line_items_manufacturer_parts_ManufacturerPartId",
                        column: x => x.ManufacturerPartId,
                        principalSchema: "platform",
                        principalTable: "manufacturer_parts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_estimate_line_items_supplier_products_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalSchema: "platform",
                        principalTable: "supplier_products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_estimate_line_items_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "platform",
                        principalTable: "suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_estimate_line_items_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "platform",
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_technician_assignments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SplitPercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_technician_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_technician_assignments_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_technician_assignments_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "platform",
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_OrganizationId_CustomerId_OpenedAtUtc",
                schema: "platform",
                table: "work_orders",
                columns: new[] { "OrganizationId", "CustomerId", "OpenedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_OrganizationId_ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders",
                columns: new[] { "OrganizationId", "ServiceAdvisorEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders",
                column: "ServiceAdvisorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_EstimateId",
                schema: "platform",
                table: "estimate_line_items",
                column: "EstimateId");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_InventoryItemId",
                schema: "platform",
                table: "estimate_line_items",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_LaborOperationId",
                schema: "platform",
                table: "estimate_line_items",
                column: "LaborOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_ManufacturerPartId",
                schema: "platform",
                table: "estimate_line_items",
                column: "ManufacturerPartId");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_OrganizationId_EstimateId_SortOrder",
                schema: "platform",
                table: "estimate_line_items",
                columns: new[] { "OrganizationId", "EstimateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_OrganizationId_InventoryItemId",
                schema: "platform",
                table: "estimate_line_items",
                columns: new[] { "OrganizationId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_OrganizationId_SupplierProductId",
                schema: "platform",
                table: "estimate_line_items",
                columns: new[] { "OrganizationId", "SupplierProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_OrganizationId_WorkOrderId",
                schema: "platform",
                table: "estimate_line_items",
                columns: new[] { "OrganizationId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_SupplierId",
                schema: "platform",
                table: "estimate_line_items",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_SupplierProductId",
                schema: "platform",
                table: "estimate_line_items",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_line_items_WorkOrderId",
                schema: "platform",
                table: "estimate_line_items",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_EmployeeId",
                schema: "platform",
                table: "work_order_technician_assignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_OrganizationId_EmployeeId",
                schema: "platform",
                table: "work_order_technician_assignments",
                columns: new[] { "OrganizationId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_OrganizationId_WorkOrderI~",
                schema: "platform",
                table: "work_order_technician_assignments",
                columns: new[] { "OrganizationId", "WorkOrderId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_WorkOrderId",
                schema: "platform",
                table: "work_order_technician_assignments",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_employees_ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders",
                column: "ServiceAdvisorEmployeeId",
                principalSchema: "platform",
                principalTable: "employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_employees_ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "estimate_line_items",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "work_order_technician_assignments",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_OrganizationId_CustomerId_OpenedAtUtc",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_OrganizationId_ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "DiagnosisFindings",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "IntakeDate",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PartsAndSuppliesNotes",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PromiseDate",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "RepairOrderNumber",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ServiceAdvisorEmployeeId",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ServiceNotes",
                schema: "platform",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "DepositTerms",
                schema: "platform",
                table: "estimates");

            migrationBuilder.DropColumn(
                name: "DiscountTotal",
                schema: "platform",
                table: "estimates");

            migrationBuilder.DropColumn(
                name: "FeesTotal",
                schema: "platform",
                table: "estimates");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                schema: "platform",
                table: "estimates");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                schema: "platform",
                table: "estimates");
        }
    }
}
