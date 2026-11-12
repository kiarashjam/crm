using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipBridgeAndHouseholds : Migration
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
                name: "LeadId",
                table: "ClubMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IncurredByMemberId",
                table: "ClubCharges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncurredByName",
                table: "ClubCharges",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubApplicationForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublicToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Intro = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SuccessMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConsentText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubApplicationForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequestedTier = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReferredByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReasonForJoining = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewerNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HouseholdRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalTrigger = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DuesSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiationChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MoneyPending = table.Column<bool>(type: "bit", nullable: false),
                    DepositPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WelcomeMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalClaimedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubHouseholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubHouseholds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubHouseholdMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    RollUpBilling = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubHouseholdMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubHouseholdMembers_ClubHouseholds_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "ClubHouseholds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMembers_OrganizationId_ContactId",
                table: "ClubMembers",
                columns: new[] { "OrganizationId", "ContactId" },
                filter: "[ContactId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubMembers_OrganizationId_LeadId",
                table: "ClubMembers",
                columns: new[] { "OrganizationId", "LeadId" },
                filter: "[LeadId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubCharges_OrganizationId_IncurredByMemberId",
                table: "ClubCharges",
                columns: new[] { "OrganizationId", "IncurredByMemberId" },
                filter: "[IncurredByMemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplicationForms_OrganizationId",
                table: "ClubApplicationForms",
                column: "OrganizationId",
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplicationForms_PublicToken",
                table: "ClubApplicationForms",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplications_OrganizationId_ContactId",
                table: "ClubApplications",
                columns: new[] { "OrganizationId", "ContactId" },
                filter: "[ContactId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplications_OrganizationId_LeadId",
                table: "ClubApplications",
                columns: new[] { "OrganizationId", "LeadId" },
                filter: "[LeadId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplications_OrganizationId_MemberId",
                table: "ClubApplications",
                columns: new[] { "OrganizationId", "MemberId" },
                filter: "[MemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplications_OrganizationId_Status_SubmittedAtUtc",
                table: "ClubApplications",
                columns: new[] { "OrganizationId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseholdMembers_HouseholdId_CreatedAtUtc",
                table: "ClubHouseholdMembers",
                columns: new[] { "HouseholdId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseholdMembers_OnePrimary",
                table: "ClubHouseholdMembers",
                column: "HouseholdId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseholdMembers_OrganizationId_ContactId",
                table: "ClubHouseholdMembers",
                columns: new[] { "OrganizationId", "ContactId" },
                unique: true,
                filter: "[ContactId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseholdMembers_OrganizationId_MemberId",
                table: "ClubHouseholdMembers",
                columns: new[] { "OrganizationId", "MemberId" },
                unique: true,
                filter: "[MemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseholds_OrganizationId",
                table: "ClubHouseholds",
                column: "OrganizationId");

            // The club's application and household documents, the couples recorded between
            // contacts and each member's contact, copied into these tables and columns in the same
            // transaction (see AddMembershipBridgeAndHouseholds.Move.cs).
            migrationBuilder.Sql(MoveSql);
        }

        /// <summary>Refuses: applications, households and who incurred a charge have lived here since this migration.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The generated Down dropped these tables and columns. Every application approved since
            // (and the member, dues and fee it points at), every household change and every charge
            // a household member ran up on the primary's account is only here, so rolling back
            // would lose them.
            throw new NotSupportedException(
                "AddMembershipBridgeAndHouseholds cannot be rolled back: applications, households and roll-up billing made since it ran are only in its tables.");
        }
    }
}
