using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iM1os.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyUsersFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "platform",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAtUtc",
                schema: "platform",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "platform",
                table: "users",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                schema: "platform",
                table: "users",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "platform",
                table: "users",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPasswordChangedAtUtc",
                schema: "platform",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "platform",
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_OrganizationId_UserId_PermissionId",
                schema: "platform",
                table: "user_permission_overrides",
                columns: new[] { "OrganizationId", "UserId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_PermissionId",
                schema: "platform",
                table: "user_permission_overrides",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_UserId",
                schema: "platform",
                table: "user_permission_overrides",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_OrganizationId_UserId_RevokedAtUtc",
                schema: "platform",
                table: "user_sessions",
                columns: new[] { "OrganizationId", "UserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_SessionKey",
                schema: "platform",
                table: "user_sessions",
                column: "SessionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId",
                schema: "platform",
                table: "user_sessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_permission_overrides",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "user_sessions",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DisabledAtUtc",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "platform",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastPasswordChangedAtUtc",
                schema: "platform",
                table: "users");
        }
    }
}
