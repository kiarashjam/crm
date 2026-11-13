using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionAndReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByMemberId",
                table: "ClubMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposerMemberId",
                table: "ClubApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByMemberId",
                table: "ClubApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeconderMemberId",
                table: "ClubApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubMemberConcerns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubMemberConcerns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubMemberRisks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FactorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousScore = table.Column<int>(type: "int", nullable: true),
                    AtRiskSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SaveActionsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SaveTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SaveMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubMemberRisks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubReferralBonuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferrerMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferredMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LoyaltyEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AwardedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubReferralBonuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubRenewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    StageSetAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StageSetByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StageSetByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContactedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RenewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RenewedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubRenewals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubRetentionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitWeight = table.Column<int>(type: "int", nullable: false),
                    SpendWeight = table.Column<int>(type: "int", nullable: false),
                    BalanceWeight = table.Column<int>(type: "int", nullable: false),
                    EventWeight = table.Column<int>(type: "int", nullable: false),
                    ConcernWeight = table.Column<int>(type: "int", nullable: false),
                    TenureWeight = table.Column<int>(type: "int", nullable: false),
                    AtRiskThreshold = table.Column<int>(type: "int", nullable: false),
                    WatchThreshold = table.Column<int>(type: "int", nullable: false),
                    SaveTask = table.Column<bool>(type: "bit", nullable: false),
                    SaveTaskDueDays = table.Column<int>(type: "int", nullable: false),
                    SaveEmail = table.Column<bool>(type: "bit", nullable: false),
                    SaveEmailPurpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SaveEmailSubject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SaveEmailBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SaveCooldownDays = table.Column<int>(type: "int", nullable: false),
                    ReferralBonus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReferralBonusPoints = table.Column<int>(type: "int", nullable: false),
                    ReferralBonusAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SponsorRule = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SponsorMinTenureMonths = table.Column<int>(type: "int", nullable: false),
                    SponsorNoOverdue = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubRetentionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubScoringRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScheduledFor = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MembersScored = table.Column<int>(type: "int", nullable: false),
                    BecameAtRisk = table.Column<int>(type: "int", nullable: false),
                    NoLongerAtRisk = table.Column<int>(type: "int", nullable: false),
                    TasksCreated = table.Column<int>(type: "int", nullable: false),
                    EmailsQueued = table.Column<int>(type: "int", nullable: false),
                    RenewalsTracked = table.Column<int>(type: "int", nullable: false),
                    Failure = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubScoringRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMembers_OrganizationId_ReferredByMemberId",
                table: "ClubMembers",
                columns: new[] { "OrganizationId", "ReferredByMemberId" },
                filter: "[ReferredByMemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplications_OrganizationId_ProposerMemberId",
                table: "ClubApplications",
                columns: new[] { "OrganizationId", "ProposerMemberId" },
                filter: "[ProposerMemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplications_OrganizationId_ReferredByMemberId",
                table: "ClubApplications",
                columns: new[] { "OrganizationId", "ReferredByMemberId" },
                filter: "[ReferredByMemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubApplications_OrganizationId_SeconderMemberId",
                table: "ClubApplications",
                columns: new[] { "OrganizationId", "SeconderMemberId" },
                filter: "[SeconderMemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubMemberConcerns_OrganizationId_MemberId_RecordedAtUtc",
                table: "ClubMemberConcerns",
                columns: new[] { "OrganizationId", "MemberId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMemberRisks_OrganizationId_Level_Score",
                table: "ClubMemberRisks",
                columns: new[] { "OrganizationId", "Level", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMemberRisks_OrganizationId_MemberId",
                table: "ClubMemberRisks",
                columns: new[] { "OrganizationId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubReferralBonuses_OrganizationId_ReferredMemberId",
                table: "ClubReferralBonuses",
                columns: new[] { "OrganizationId", "ReferredMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubReferralBonuses_OrganizationId_ReferrerMemberId",
                table: "ClubReferralBonuses",
                columns: new[] { "OrganizationId", "ReferrerMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubRenewals_OrganizationId_EndsAtUtc",
                table: "ClubRenewals",
                columns: new[] { "OrganizationId", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubRenewals_OrganizationId_MemberId_Source_EndsAtUtc",
                table: "ClubRenewals",
                columns: new[] { "OrganizationId", "MemberId", "Source", "EndsAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubRetentionSettings_OrganizationId",
                table: "ClubRetentionSettings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubScoringRuns_OrganizationId_ScheduledFor",
                table: "ClubScoringRuns",
                columns: new[] { "OrganizationId", "ScheduledFor" },
                unique: true,
                filter: "[ScheduledFor] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubScoringRuns_OrganizationId_StartedAtUtc",
                table: "ClubScoringRuns",
                columns: new[] { "OrganizationId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubMemberConcerns");

            migrationBuilder.DropTable(
                name: "ClubMemberRisks");

            migrationBuilder.DropTable(
                name: "ClubReferralBonuses");

            migrationBuilder.DropTable(
                name: "ClubRenewals");

            migrationBuilder.DropTable(
                name: "ClubRetentionSettings");

            migrationBuilder.DropTable(
                name: "ClubScoringRuns");

            migrationBuilder.DropIndex(
                name: "IX_ClubMembers_OrganizationId_ReferredByMemberId",
                table: "ClubMembers");

            migrationBuilder.DropIndex(
                name: "IX_ClubApplications_OrganizationId_ProposerMemberId",
                table: "ClubApplications");

            migrationBuilder.DropIndex(
                name: "IX_ClubApplications_OrganizationId_ReferredByMemberId",
                table: "ClubApplications");

            migrationBuilder.DropIndex(
                name: "IX_ClubApplications_OrganizationId_SeconderMemberId",
                table: "ClubApplications");

            migrationBuilder.DropColumn(
                name: "ReferredByMemberId",
                table: "ClubMembers");

            migrationBuilder.DropColumn(
                name: "ProposerMemberId",
                table: "ClubApplications");

            migrationBuilder.DropColumn(
                name: "ReferredByMemberId",
                table: "ClubApplications");

            migrationBuilder.DropColumn(
                name: "SeconderMemberId",
                table: "ClubApplications");
        }
    }
}
