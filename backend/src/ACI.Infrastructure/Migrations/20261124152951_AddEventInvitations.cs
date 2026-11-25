using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubEventInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SendId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReminderTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Answer = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemindedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReminderMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEventInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEventInvitations_ClubEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "ClubEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubEventInvitationSends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AudienceJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    LettersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Recipients = table.Column<int>(type: "int", nullable: false),
                    LeftOut = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEventInvitationSends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEventInvitationSends_ClubEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "ClubEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventInvitations_EventId_MemberId",
                table: "ClubEventInvitations",
                columns: new[] { "EventId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventInvitations_OrganizationId_MemberId",
                table: "ClubEventInvitations",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventInvitations_ReminderTokenHash",
                table: "ClubEventInvitations",
                column: "ReminderTokenHash",
                unique: true,
                filter: "[ReminderTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventInvitations_TokenHash",
                table: "ClubEventInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventInvitationSends_EventId",
                table: "ClubEventInvitationSends",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventInvitationSends_OrganizationId_EventId_CreatedAtUtc",
                table: "ClubEventInvitationSends",
                columns: new[] { "OrganizationId", "EventId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubEventInvitations");

            migrationBuilder.DropTable(
                name: "ClubEventInvitationSends");
        }
    }
}
