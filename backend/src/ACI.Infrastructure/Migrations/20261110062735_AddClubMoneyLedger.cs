using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClubMoneyLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubDonations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cause = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Acknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReversesDonationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubDonations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubDonations_ClubDonations_ReversesDonationId",
                        column: x => x.ReversesDonationId,
                        principalTable: "ClubDonations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClubGiftCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FaceValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PurchaserMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastTransactionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubGiftCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubHouseAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEntryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubHouseAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReversesPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubPayments_ClubPayments_ReversesPaymentId",
                        column: x => x.ReversesPaymentId,
                        principalTable: "ClubPayments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClubSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Cadence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextChargeAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastChargedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubTabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MemberTier = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ServerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LoyaltyEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PointsAwarded = table.Column<int>(type: "int", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubTabs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubGiftCardTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GiftCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppliedTo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RedeemedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubGiftCardTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubGiftCardTransactions_ClubGiftCards_GiftCardId",
                        column: x => x.GiftCardId,
                        principalTable: "ClubGiftCards",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClubCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReversesChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TabId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillingPeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubCharges_ClubCharges_ReversesChargeId",
                        column: x => x.ReversesChargeId,
                        principalTable: "ClubCharges",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClubCharges_ClubSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "ClubSubscriptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClubCharges_ClubTabs_TabId",
                        column: x => x.TabId,
                        principalTable: "ClubTabs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClubTabItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AddedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemovedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubTabItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubTabItems_ClubTabs_TabId",
                        column: x => x.TabId,
                        principalTable: "ClubTabs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClubPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReversalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubPaymentAllocations_ClubCharges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "ClubCharges",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClubPaymentAllocations_ClubPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "ClubPayments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClubPaymentAllocations_ClubPayments_ReversalId",
                        column: x => x.ReversalId,
                        principalTable: "ClubPayments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCharges_OrganizationId_IdempotencyKey",
                table: "ClubCharges",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubCharges_OrganizationId_MemberId_PostedAtUtc",
                table: "ClubCharges",
                columns: new[] { "OrganizationId", "MemberId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCharges_ReversesChargeId",
                table: "ClubCharges",
                column: "ReversesChargeId",
                unique: true,
                filter: "[ReversesChargeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubCharges_SubscriptionId_BillingPeriodStartUtc",
                table: "ClubCharges",
                columns: new[] { "SubscriptionId", "BillingPeriodStartUtc" },
                unique: true,
                filter: "[SubscriptionId] IS NOT NULL AND [BillingPeriodStartUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubCharges_TabId",
                table: "ClubCharges",
                column: "TabId",
                unique: true,
                filter: "[TabId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubDonations_OrganizationId_ReceivedAtUtc",
                table: "ClubDonations",
                columns: new[] { "OrganizationId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubDonations_ReversesDonationId",
                table: "ClubDonations",
                column: "ReversesDonationId",
                unique: true,
                filter: "[ReversesDonationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubGiftCards_OrganizationId_Code",
                table: "ClubGiftCards",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubGiftCardTransactions_GiftCardId_AtUtc",
                table: "ClubGiftCardTransactions",
                columns: new[] { "GiftCardId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubGiftCardTransactions_OneExpiry",
                table: "ClubGiftCardTransactions",
                column: "GiftCardId",
                unique: true,
                filter: "[Type] = N'Expiry'");

            migrationBuilder.CreateIndex(
                name: "IX_ClubGiftCardTransactions_OneVoid",
                table: "ClubGiftCardTransactions",
                column: "GiftCardId",
                unique: true,
                filter: "[Type] = N'Void'");

            migrationBuilder.CreateIndex(
                name: "IX_ClubGiftCardTransactions_OrganizationId",
                table: "ClubGiftCardTransactions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubGiftCardTransactions_OrganizationId_IdempotencyKey",
                table: "ClubGiftCardTransactions",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseAccounts_MemberId",
                table: "ClubHouseAccounts",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseAccounts_OrganizationId",
                table: "ClubHouseAccounts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentAllocations_ChargeId",
                table: "ClubPaymentAllocations",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentAllocations_OrganizationId",
                table: "ClubPaymentAllocations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentAllocations_PaymentId",
                table: "ClubPaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentAllocations_ReversalId",
                table: "ClubPaymentAllocations",
                column: "ReversalId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPayments_OneVoid",
                table: "ClubPayments",
                column: "ReversesPaymentId",
                unique: true,
                filter: "[ReversesPaymentId] IS NOT NULL AND [EntryType] = N'Void'");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPayments_OrganizationId_IdempotencyKey",
                table: "ClubPayments",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPayments_OrganizationId_MemberId_ReceivedAtUtc",
                table: "ClubPayments",
                columns: new[] { "OrganizationId", "MemberId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubPayments_ReversesPaymentId",
                table: "ClubPayments",
                column: "ReversesPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubSubscriptions_OrganizationId_MemberId",
                table: "ClubSubscriptions",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubTabItems_OrganizationId",
                table: "ClubTabItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubTabItems_TabId",
                table: "ClubTabItems",
                column: "TabId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubTabs_OrganizationId_Status",
                table: "ClubTabs",
                columns: new[] { "OrganizationId", "Status" });

            // The club's existing money documents, copied into these tables in the same
            // transaction (see AddClubMoneyLedger.Move.cs).
            migrationBuilder.Sql(MoveDocumentsSql);
        }

        /// <summary>Refuses: the club's money has lived in these tables since this migration.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The generated Down dropped the ledger's tables. Every charge, payment, gift card
            // and tab posted since the move is only there (the documents it copied from stop at
            // the move), so rolling back would lose the club's books since then.
            throw new NotSupportedException(
                "AddClubMoneyLedger cannot be rolled back: every charge, payment and gift card posted since it ran is only in its tables.");
        }
    }
}
