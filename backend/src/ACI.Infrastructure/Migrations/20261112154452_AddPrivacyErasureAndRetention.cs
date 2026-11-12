using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyErasureAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ErasedAtUtc",
                table: "ClubMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PersonErasures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ErasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErasedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonErasures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivacySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ControllerAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ControllerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DpoContact = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MarketingConsent = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MarketingConsentText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LeadRetentionMonths = table.Column<int>(type: "int", nullable: true),
                    MemberRetentionYears = table.Column<int>(type: "int", nullable: true),
                    MailRetentionMonths = table.Column<int>(type: "int", nullable: true),
                    DetailRetentionMonths = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionRuns",
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
                    StartedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeadsDeleted = table.Column<int>(type: "int", nullable: false),
                    MembersAnonymised = table.Column<int>(type: "int", nullable: false),
                    MessagesPurged = table.Column<int>(type: "int", nullable: false),
                    DetailsPurged = table.Column<int>(type: "int", nullable: false),
                    Failures = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonErasures_OrganizationId_ErasedAtUtc",
                table: "PersonErasures",
                columns: new[] { "OrganizationId", "ErasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonErasures_OrganizationId_Number",
                table: "PersonErasures",
                columns: new[] { "OrganizationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacySettings_OrganizationId",
                table: "PrivacySettings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRuns_OrganizationId_ScheduledFor",
                table: "RetentionRuns",
                columns: new[] { "OrganizationId", "ScheduledFor" },
                unique: true,
                filter: "[ScheduledFor] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRuns_OrganizationId_StartedAtUtc",
                table: "RetentionRuns",
                columns: new[] { "OrganizationId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonErasures");

            migrationBuilder.DropTable(
                name: "PrivacySettings");

            migrationBuilder.DropTable(
                name: "RetentionRuns");

            migrationBuilder.DropColumn(
                name: "ErasedAtUtc",
                table: "ClubMembers");
        }
    }
}
