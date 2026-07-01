using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierConnectorSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FitmentScheduleCadenceMinutes",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 1440);

            migrationBuilder.AddColumn<int>(
                name: "FitmentScheduleDelayMilliseconds",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 250);

            migrationBuilder.AddColumn<int>(
                name: "FitmentScheduleFitmentLimit",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FitmentScheduleMaxSkus",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FitmentSourceBaseUrl",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImportFitmentOnSchedule",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastFitmentScheduledAtUtc",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMasterFileScheduledAtUtc",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MasterFileScheduleCadenceMinutes",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 1440);

            migrationBuilder.AddColumn<int>(
                name: "MasterFileScheduleMaxItems",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FitmentScheduleCadenceMinutes",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "FitmentScheduleDelayMilliseconds",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "FitmentScheduleFitmentLimit",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "FitmentScheduleMaxSkus",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "FitmentSourceBaseUrl",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "ImportFitmentOnSchedule",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "LastFitmentScheduledAtUtc",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "LastMasterFileScheduledAtUtc",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "MasterFileScheduleCadenceMinutes",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "MasterFileScheduleMaxItems",
                schema: "platform",
                table: "supplier_connector_configurations");
        }
    }
}
