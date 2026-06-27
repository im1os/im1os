using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIm1PlatformControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_audit_events",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorPlatformUserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TargetOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    NewValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_events",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorPlatformUserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EventType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_subscriptions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BillingStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    TrialExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BillingProviderCustomerId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_tenants",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SubscriptionPlan = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CurrentVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HealthStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActiveUsers = table.Column<int>(type: "integer", nullable: false),
                    Locations = table.Column<int>(type: "integer", nullable: false),
                    TrialExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BillingStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProvisioningStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_users",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_module_entitlements",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EnabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnabledByPlatformUserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_module_entitlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "welcome_emails",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_welcome_emails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_events_TargetOrganizationId_OccurredAtUtc",
                schema: "platform",
                table: "platform_audit_events",
                columns: new[] { "TargetOrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_events_CorrelationId",
                schema: "platform",
                table: "platform_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_events_EventType",
                schema: "platform",
                table: "platform_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_platform_events_TargetOrganizationId_OccurredAtUtc",
                schema: "platform",
                table: "platform_events",
                columns: new[] { "TargetOrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_subscriptions_OrganizationId",
                schema: "platform",
                table: "platform_subscriptions",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_tenants_OrganizationId",
                schema: "platform",
                table: "platform_tenants",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_tenants_Slug",
                schema: "platform",
                table: "platform_tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_tenants_Status",
                schema: "platform",
                table: "platform_tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_NormalizedEmail",
                schema: "platform",
                table: "platform_users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_module_entitlements_OrganizationId_ModuleKey",
                schema: "platform",
                table: "tenant_module_entitlements",
                columns: new[] { "OrganizationId", "ModuleKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_welcome_emails_OrganizationId_CreatedAtUtc",
                schema: "platform",
                table: "welcome_emails",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_audit_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "platform_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "platform_subscriptions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "platform_tenants",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "platform_users",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "tenant_module_entitlements",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "welcome_emails",
                schema: "platform");
        }
    }
}
