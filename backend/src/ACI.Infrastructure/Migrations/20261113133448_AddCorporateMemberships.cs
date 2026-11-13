using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubCorporateMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NomineeCharges = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RenewsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: false),
                    NotRenewing = table.Column<bool>(type: "bit", nullable: false),
                    NotRenewingReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastRenewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DuesSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillingContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EndedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NomineesChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubCorporateMemberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubCorporateNominees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorporateMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AddedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemovedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RemovedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplacedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubCorporateNominees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubCorporateNominees_ClubCorporateMemberships_CorporateMembershipId",
                        column: x => x.CorporateMembershipId,
                        principalTable: "ClubCorporateMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCorporateMemberships_OrganizationId_CompanyId",
                table: "ClubCorporateMemberships",
                columns: new[] { "OrganizationId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCorporateMemberships_OrganizationId_Status_RenewsAtUtc",
                table: "ClubCorporateMemberships",
                columns: new[] { "OrganizationId", "Status", "RenewsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCorporateNominees_CorporateMembershipId",
                table: "ClubCorporateNominees",
                column: "CorporateMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubCorporateNominees_OrganizationId_CorporateMembershipId",
                table: "ClubCorporateNominees",
                columns: new[] { "OrganizationId", "CorporateMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubCorporateNominees_OrganizationId_MemberId",
                table: "ClubCorporateNominees",
                columns: new[] { "OrganizationId", "MemberId" },
                unique: true,
                filter: "[RemovedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubCorporateNominees");

            migrationBuilder.DropTable(
                name: "ClubCorporateMemberships");
        }
    }
}
