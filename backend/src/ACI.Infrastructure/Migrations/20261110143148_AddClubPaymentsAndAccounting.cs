using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClubPaymentsAndAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "ClubPayments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentId",
                table: "ClubPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubBankImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Transactions = table.Column<int>(type: "int", nullable: false),
                    Matched = table.Column<int>(type: "int", nullable: false),
                    Unmatched = table.Column<int>(type: "int", nullable: false),
                    AlreadyImported = table.Column<int>(type: "int", nullable: false),
                    Skipped = table.Column<int>(type: "int", nullable: false),
                    AmountMatched = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImportedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubBankImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubOnlinePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChargeIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StatementFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatementToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckoutSessionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SetupIntentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderChargeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MethodType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AmountReceived = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LedgerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubOnlinePayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubPaymentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentMethodId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentMethodType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    ExpMonth = table.Column<int>(type: "int", nullable: true),
                    ExpYear = table.Column<int>(type: "int", nullable: true),
                    SavedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutoPayDues = table.Column<bool>(type: "bit", nullable: false),
                    AutoPayAgreedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutoPayAgreedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPaymentProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubPaymentReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    QrReference = table.Column<string>(type: "nvarchar(27)", maxLength: 27, nullable: true),
                    ScorReference = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPaymentReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubPaymentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StripeSecretKeyProtected = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StripeKeyHint = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    StripeWebhookSecretProtected = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StripeOfferTwint = table.Column<bool>(type: "bit", nullable: false),
                    StripeOfferSepaDebit = table.Column<bool>(type: "bit", nullable: false),
                    WebhookKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StripeCheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    QrIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    QrReferencePrefix = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    CreditorName = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: true),
                    CreditorStreet = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: true),
                    CreditorBuildingNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CreditorPostalCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CreditorTown = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    CreditorCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AccountsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPaymentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubProviderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ObjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubProviderEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubBankTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    BookedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DebtorName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    DebtorIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LedgerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubBankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubBankTransactions_ClubBankImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "ClubBankImports",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubBankImports_OrganizationId_ImportedAtUtc",
                table: "ClubBankImports",
                columns: new[] { "OrganizationId", "ImportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubBankTransactions_ImportId",
                table: "ClubBankTransactions",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubBankTransactions_OrganizationId_BankReference",
                table: "ClubBankTransactions",
                columns: new[] { "OrganizationId", "BankReference" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubBankTransactions_OrganizationId_Status",
                table: "ClubBankTransactions",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubOnlinePayments_OrganizationId_AttemptKey",
                table: "ClubOnlinePayments",
                columns: new[] { "OrganizationId", "AttemptKey" },
                unique: true,
                filter: "[AttemptKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubOnlinePayments_OrganizationId_CheckoutSessionId",
                table: "ClubOnlinePayments",
                columns: new[] { "OrganizationId", "CheckoutSessionId" },
                unique: true,
                filter: "[CheckoutSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubOnlinePayments_OrganizationId_MemberId_CreatedAtUtc",
                table: "ClubOnlinePayments",
                columns: new[] { "OrganizationId", "MemberId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubOnlinePayments_OrganizationId_PaymentIntentId",
                table: "ClubOnlinePayments",
                columns: new[] { "OrganizationId", "PaymentIntentId" },
                unique: true,
                filter: "[PaymentIntentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubOnlinePayments_OrganizationId_Status",
                table: "ClubOnlinePayments",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentProfiles_OrganizationId_MemberId_Provider",
                table: "ClubPaymentProfiles",
                columns: new[] { "OrganizationId", "MemberId", "Provider" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentReferences_OrganizationId_ChargeId",
                table: "ClubPaymentReferences",
                columns: new[] { "OrganizationId", "ChargeId" },
                unique: true,
                filter: "[ChargeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentReferences_OrganizationId_MemberId_PeriodStartUtc_PeriodEndUtc",
                table: "ClubPaymentReferences",
                columns: new[] { "OrganizationId", "MemberId", "PeriodStartUtc", "PeriodEndUtc" },
                unique: true,
                filter: "[Purpose] = N'statement'");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentReferences_OrganizationId_Number",
                table: "ClubPaymentReferences",
                columns: new[] { "OrganizationId", "Number" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentReferences_OrganizationId_QrReference",
                table: "ClubPaymentReferences",
                columns: new[] { "OrganizationId", "QrReference" },
                unique: true,
                filter: "[QrReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentReferences_OrganizationId_ScorReference",
                table: "ClubPaymentReferences",
                columns: new[] { "OrganizationId", "ScorReference" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentSettings_OrganizationId",
                table: "ClubPaymentSettings",
                column: "OrganizationId",
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPaymentSettings_WebhookKey",
                table: "ClubPaymentSettings",
                column: "WebhookKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubProviderEvents_OrganizationId_Provider_EventId",
                table: "ClubProviderEvents",
                columns: new[] { "OrganizationId", "Provider", "EventId" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubBankTransactions");

            migrationBuilder.DropTable(
                name: "ClubOnlinePayments");

            migrationBuilder.DropTable(
                name: "ClubPaymentProfiles");

            migrationBuilder.DropTable(
                name: "ClubPaymentReferences");

            migrationBuilder.DropTable(
                name: "ClubPaymentSettings");

            migrationBuilder.DropTable(
                name: "ClubProviderEvents");

            migrationBuilder.DropTable(
                name: "ClubBankImports");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ClubPayments");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentId",
                table: "ClubPayments");
        }
    }
}
