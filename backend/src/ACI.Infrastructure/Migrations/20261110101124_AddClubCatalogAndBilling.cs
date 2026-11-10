using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// The club's price catalogue (with each workspace's tiers carried in: see the .Tiers part),
    /// and what billing records: the daily runs, payment reminders, statements sent, files that
    /// go with an outbox email, a member's lapse by the billing, and the catalogue item, VAT
    /// rate and billing day a charge, tab item or subscription takes from the catalogue.
    /// </summary>
    /// <remarks>
    /// Only adds: new tables, and nullable columns that existing rows leave empty. The
    /// ledger's rows are not touched: a subscription from before keeps billing on its next
    /// date's day, and a charge from before has no VAT rate recorded, as before.
    /// </remarks>
    public partial class AddClubCatalogAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                table: "ClubTabItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "ClubTabItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingAnchorDay",
                table: "ClubSubscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                table: "ClubSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingLapsedAtUtc",
                table: "ClubMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusBeforeLapse",
                table: "ClubMembers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                table: "ClubCharges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "ClubCharges",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubBillingRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScheduledFor = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubscriptionsBilled = table.Column<int>(type: "int", nullable: false),
                    ChargesPosted = table.Column<int>(type: "int", nullable: false),
                    AmountBilled = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OverdueCharges = table.Column<int>(type: "int", nullable: false),
                    SubscriptionsPastDue = table.Column<int>(type: "int", nullable: false),
                    RemindersQueued = table.Column<int>(type: "int", nullable: false),
                    MembersLapsed = table.Column<int>(type: "int", nullable: false),
                    MembersReinstated = table.Column<int>(type: "int", nullable: false),
                    StatementsQueued = table.Column<int>(type: "int", nullable: false),
                    Failures = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    Steps = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubBillingRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Cadence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InitiationFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LoyaltyMultiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Benefits = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChargeKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubPaymentReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Step = table.Column<int>(type: "int", nullable: false),
                    DaysAfterDue = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPaymentReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubPaymentReminders_ClubCharges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "ClubCharges",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClubStatementDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubStatementDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboundAttachments_OutboundMessages_OutboundMessageId",
                        column: x => x.OutboundMessageId,
                        principalTable: "OutboundMessages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCharges_OrganizationId_DueAtUtc",
                table: "ClubCharges",
                columns: new[] { "OrganizationId", "DueAtUtc" },
                filter: "[DueAtUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubBillingRuns_OrganizationId_ScheduledFor",
                table: "ClubBillingRuns",
                columns: new[] { "OrganizationId", "ScheduledFor" },
                unique: true,
                filter: "[ScheduledFor] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubBillingRuns_OrganizationId_StartedAtUtc",
                table: "ClubBillingRuns",
                columns: new[] { "OrganizationId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCatalogItems_OneTierPerName",
                table: "ClubCatalogItems",
                columns: new[] { "OrganizationId", "Name" },
                unique: true,
                filter: "[Kind] = N'Membership'");

            migrationBuilder.CreateIndex(
                name: "IX_ClubCatalogItems_OrganizationId_Kind_SortOrder",
                table: "ClubCatalogItems",
                columns: new[] { "OrganizationId", "Kind", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentReminders_ChargeId_Step",
                table: "ClubPaymentReminders",
                columns: new[] { "ChargeId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentReminders_OrganizationId_MemberId",
                table: "ClubPaymentReminders",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubStatementDeliveries_OneMonthly",
                table: "ClubStatementDeliveries",
                columns: new[] { "MemberId", "PeriodStartUtc" },
                unique: true,
                filter: "[Source] = N'monthly'");

            migrationBuilder.CreateIndex(
                name: "IX_ClubStatementDeliveries_OrganizationId_MemberId_PeriodStartUtc",
                table: "ClubStatementDeliveries",
                columns: new[] { "OrganizationId", "MemberId", "PeriodStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundAttachments_OutboundMessageId",
                table: "OutboundAttachments",
                column: "OutboundMessageId");

            migrationBuilder.Sql(CarryOverTiersSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubBillingRuns");

            migrationBuilder.DropTable(
                name: "ClubCatalogItems");

            migrationBuilder.DropTable(
                name: "ClubPaymentReminders");

            migrationBuilder.DropTable(
                name: "ClubStatementDeliveries");

            migrationBuilder.DropTable(
                name: "OutboundAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ClubCharges_OrganizationId_DueAtUtc",
                table: "ClubCharges");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "ClubTabItems");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "ClubTabItems");

            migrationBuilder.DropColumn(
                name: "BillingAnchorDay",
                table: "ClubSubscriptions");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "ClubSubscriptions");

            migrationBuilder.DropColumn(
                name: "BillingLapsedAtUtc",
                table: "ClubMembers");

            migrationBuilder.DropColumn(
                name: "StatusBeforeLapse",
                table: "ClubMembers");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "ClubCharges");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "ClubCharges");
        }
    }
}
