using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdentityExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                schema: "platform",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                schema: "platform",
                table: "users",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifiedAtUtc",
                schema: "platform",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "platform",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "en-US");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEndAtUtc",
                schema: "platform",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                schema: "platform",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MfaMethod",
                schema: "platform",
                table: "users",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaSecretProtected",
                schema: "platform",
                table: "users",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "platform",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "platform",
                table: "users",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                schema: "platform",
                table: "users",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "platform",
                table: "users",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "America/Chicago");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                schema: "platform",
                table: "organizations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardingCompletedAtUtc",
                schema: "platform",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "business_onboardings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BusinessEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BusinessHoursJson = table.Column<string>(type: "jsonb", nullable: false),
                    LaborRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SuppliersSkipped = table.Column<bool>(type: "boolean", nullable: false),
                    MerchantServicesSkipped = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedSteps = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_onboardings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_requests",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_identity_events",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_identity_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_invitations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_invitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_onboardings_OrganizationId",
                schema: "platform",
                table: "business_onboardings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_requests_OrganizationId_UserId",
                schema: "platform",
                table: "password_reset_requests",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_requests_TokenHash",
                schema: "platform",
                table: "password_reset_requests",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_identity_events_EventType",
                schema: "platform",
                table: "tenant_identity_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_identity_events_OrganizationId_OccurredAtUtc",
                schema: "platform",
                table: "tenant_identity_events",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_OrganizationId_UserId",
                schema: "platform",
                table: "user_invitations",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_TokenHash",
                schema: "platform",
                table: "user_invitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_onboardings",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "password_reset_requests",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "tenant_identity_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "user_invitations",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LockoutEndAtUtc",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaMethod",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaSecretProtected",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PinHash",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                schema: "platform",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAtUtc",
                schema: "platform",
                table: "organizations");
        }
    }
}
