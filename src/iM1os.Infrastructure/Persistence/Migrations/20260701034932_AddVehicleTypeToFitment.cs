using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleTypeToFitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VehicleClass",
                schema: "platform",
                table: "supplier_fitment_records",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                schema: "platform",
                table: "supplier_fitment_records",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleClass",
                schema: "platform",
                table: "global_vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                schema: "platform",
                table: "global_vehicles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_fitment_records_VehicleType",
                schema: "platform",
                table: "supplier_fitment_records",
                column: "VehicleType");

            migrationBuilder.CreateIndex(
                name: "IX_global_vehicles_VehicleType",
                schema: "platform",
                table: "global_vehicles",
                column: "VehicleType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_fitment_records_VehicleType",
                schema: "platform",
                table: "supplier_fitment_records");

            migrationBuilder.DropIndex(
                name: "IX_global_vehicles_VehicleType",
                schema: "platform",
                table: "global_vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleClass",
                schema: "platform",
                table: "supplier_fitment_records");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                schema: "platform",
                table: "supplier_fitment_records");

            migrationBuilder.DropColumn(
                name: "VehicleClass",
                schema: "platform",
                table: "global_vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                schema: "platform",
                table: "global_vehicles");
        }
    }
}
