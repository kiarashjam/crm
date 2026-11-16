using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MemberBooking",
                table: "ClubResources",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "off");

            migrationBuilder.CreateTable(
                name: "MemberPortalAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitationsSent = table.Column<int>(type: "int", nullable: false),
                    FirstSignInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSignInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPortalAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberPortalLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPortalLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberPortalSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Via = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPortalSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberPortalSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    PortalKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MemberCancelHours = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPortalSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalAccounts_OrganizationId_MemberId",
                table: "MemberPortalAccounts",
                columns: new[] { "OrganizationId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalLinks_ExpiresAtUtc",
                table: "MemberPortalLinks",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalLinks_OrganizationId_MemberId",
                table: "MemberPortalLinks",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalLinks_TokenHash",
                table: "MemberPortalLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalSessions_ExpiresAtUtc",
                table: "MemberPortalSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalSessions_OrganizationId_MemberId",
                table: "MemberPortalSessions",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalSettings_OrganizationId",
                table: "MemberPortalSettings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberPortalSettings_PortalKey",
                table: "MemberPortalSettings",
                column: "PortalKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberPortalAccounts");

            migrationBuilder.DropTable(
                name: "MemberPortalLinks");

            migrationBuilder.DropTable(
                name: "MemberPortalSessions");

            migrationBuilder.DropTable(
                name: "MemberPortalSettings");

            migrationBuilder.DropColumn(
                name: "MemberBooking",
                table: "ClubResources");
        }
    }
}
