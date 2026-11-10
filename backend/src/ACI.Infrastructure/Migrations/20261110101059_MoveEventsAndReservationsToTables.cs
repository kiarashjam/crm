using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveEventsAndReservationsToTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    MemberPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GuestPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    NoShowFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CoverEmoji = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UnnamedSeats = table.Column<int>(type: "int", nullable: false),
                    UnnamedWaitlist = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ListChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MemberEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PartySize = table.Column<int>(type: "int", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DepositChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepositPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepositReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepositWaived = table.Column<bool>(type: "bit", nullable: false),
                    DepositWaivedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DepositWaivedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NoShowFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NoShowChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NoShowFeeReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BookingMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NoShowFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BookingsChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubEventRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    TicketTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HostMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HostName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PromotedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedInByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NoShowAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TicketPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChargeReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NoShowFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NoShowChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NoShowFeeReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PointsAwarded = table.Column<int>(type: "int", nullable: true),
                    LoyaltyEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImportedFromId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEventRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEventRegistrations_ClubEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "ClubEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventRegistrations_EventId_Status_RegisteredAtUtc",
                table: "ClubEventRegistrations",
                columns: new[] { "EventId", "Status", "RegisteredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventRegistrations_OrganizationId_HostMemberId",
                table: "ClubEventRegistrations",
                columns: new[] { "OrganizationId", "HostMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventRegistrations_OrganizationId_MemberId",
                table: "ClubEventRegistrations",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEvents_OrganizationId_StartAtUtc",
                table: "ClubEvents",
                columns: new[] { "OrganizationId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubReservations_OrganizationId_MemberId",
                table: "ClubReservations",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubReservations_OrganizationId_ResourceId_StartAtUtc",
                table: "ClubReservations",
                columns: new[] { "OrganizationId", "ResourceId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubResources_OrganizationId",
                table: "ClubResources",
                column: "OrganizationId");

            // The club's existing event and booking documents, copied into these tables in the
            // same transaction (see MoveEventsAndReservationsToTables.Move.cs).
            migrationBuilder.Sql(MoveDocumentsSql);
        }

        /// <summary>Refuses: the club's events and bookings have lived in these tables since this migration.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The generated Down dropped these tables. Every registration, check-in and booking
            // made since the copy is only here (the documents it copied from stop at the copy),
            // and so are the links from tickets, deposits and no-show fees to their charges, so
            // rolling back would lose them.
            throw new NotSupportedException(
                "MoveEventsAndReservationsToTables cannot be rolled back: every registration and booking made since it ran is only in its tables.");
        }
    }
}
