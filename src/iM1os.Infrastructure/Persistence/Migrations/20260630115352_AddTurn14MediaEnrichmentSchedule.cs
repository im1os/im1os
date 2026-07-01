using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTurn14MediaEnrichmentSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImportMediaOnSchedule",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMediaScheduledAtUtc",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaScheduleCadenceMinutes",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 1440);

            migrationBuilder.AddColumn<int>(
                name: "MediaScheduleDelayMilliseconds",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 750);

            migrationBuilder.AddColumn<int>(
                name: "MediaScheduleMaxItems",
                schema: "platform",
                table: "supplier_connector_configurations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportMediaOnSchedule",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "LastMediaScheduledAtUtc",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "MediaScheduleCadenceMinutes",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "MediaScheduleDelayMilliseconds",
                schema: "platform",
                table: "supplier_connector_configurations");

            migrationBuilder.DropColumn(
                name: "MediaScheduleMaxItems",
                schema: "platform",
                table: "supplier_connector_configurations");
        }
    }
}
