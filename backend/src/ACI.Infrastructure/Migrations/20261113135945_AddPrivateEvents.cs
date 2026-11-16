using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrivateEventId",
                table: "ClubReservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubPrivateEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuestCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BillToAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DepositChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepositRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepositSettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoicedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPrivateEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubPrivateEventGuests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrivateEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPrivateEventGuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubPrivateEventGuests_ClubPrivateEvents_PrivateEventId",
                        column: x => x.PrivateEventId,
                        principalTable: "ClubPrivateEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubPrivateEventLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrivateEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPrivateEventLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubPrivateEventLines_ClubPrivateEvents_PrivateEventId",
                        column: x => x.PrivateEventId,
                        principalTable: "ClubPrivateEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubReservations_OrganizationId_PrivateEventId",
                table: "ClubReservations",
                columns: new[] { "OrganizationId", "PrivateEventId" },
                filter: "[PrivateEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEventGuests_OrganizationId_MemberId",
                table: "ClubPrivateEventGuests",
                columns: new[] { "OrganizationId", "MemberId" },
                filter: "[MemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEventGuests_OrganizationId_PrivateEventId",
                table: "ClubPrivateEventGuests",
                columns: new[] { "OrganizationId", "PrivateEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEventGuests_PrivateEventId",
                table: "ClubPrivateEventGuests",
                column: "PrivateEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEventLines_OrganizationId_PrivateEventId",
                table: "ClubPrivateEventLines",
                columns: new[] { "OrganizationId", "PrivateEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEventLines_PrivateEventId",
                table: "ClubPrivateEventLines",
                column: "PrivateEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEvents_ContractId",
                table: "ClubPrivateEvents",
                column: "ContractId",
                filter: "[ContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEvents_OrganizationId_CompanyId",
                table: "ClubPrivateEvents",
                columns: new[] { "OrganizationId", "CompanyId" },
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEvents_OrganizationId_ContactId",
                table: "ClubPrivateEvents",
                columns: new[] { "OrganizationId", "ContactId" },
                filter: "[ContactId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubPrivateEvents_OrganizationId_StartAtUtc",
                table: "ClubPrivateEvents",
                columns: new[] { "OrganizationId", "StartAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubPrivateEventGuests");

            migrationBuilder.DropTable(
                name: "ClubPrivateEventLines");

            migrationBuilder.DropTable(
                name: "ClubPrivateEvents");

            migrationBuilder.DropIndex(
                name: "IX_ClubReservations_OrganizationId_PrivateEventId",
                table: "ClubReservations");

            migrationBuilder.DropColumn(
                name: "PrivateEventId",
                table: "ClubReservations");
        }
    }
}
