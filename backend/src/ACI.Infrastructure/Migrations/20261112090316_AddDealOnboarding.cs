using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ClubMembers.ContactId is made by two migrations: AddMembershipBridgeAndHouseholds
            // (the club's link from a member to their contact) and AddDealOnboarding (the
            // contact a member joined through). Both mean the same link, so whichever runs first
            // makes it and the other finds it there: a database made from scratch runs the
            // bridge first, while one that already had AddDealOnboarding (it shipped first)
            // gets the bridge after it.
            migrationBuilder.Sql("IF COL_LENGTH(N'dbo.ClubMembers', N'ContactId') IS NULL ALTER TABLE [ClubMembers] ADD [ContactId] uniqueidentifier NULL;");

            migrationBuilder.AddColumn<Guid>(
                name: "DealId",
                table: "ClubMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StartUndetermined",
                table: "ClubMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartsOnUtc",
                table: "ClubMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DealOnboardings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClubMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    WelcomeMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartsOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartUndetermined = table.Column<bool>(type: "bit", nullable: false),
                    EmailSubject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmailHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StepsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealOnboardings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WebhookUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebhookSecret = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailSubject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmailHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    AddToClubMembers = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealOnboardings_OrganizationId_DealId_CreatedAtUtc",
                table: "DealOnboardings",
                columns: new[] { "OrganizationId", "DealId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingSettings_OrganizationId",
                table: "OnboardingSettings",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealOnboardings");

            migrationBuilder.DropTable(
                name: "OnboardingSettings");

            // ContactId stays: AddMembershipBridgeAndHouseholds uses it too, and drops it in its own Down.
            migrationBuilder.DropColumn(
                name: "DealId",
                table: "ClubMembers");

            migrationBuilder.DropColumn(
                name: "StartUndetermined",
                table: "ClubMembers");

            migrationBuilder.DropColumn(
                name: "StartsOnUtc",
                table: "ClubMembers");
        }
    }
}
