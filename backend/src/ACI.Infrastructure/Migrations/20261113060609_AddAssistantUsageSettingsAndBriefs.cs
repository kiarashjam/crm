using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantUsageSettingsAndBriefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MorningBriefEmail",
                table: "UserSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MorningBriefOff",
                table: "UserSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiUsage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Calls = table.Column<int>(type: "int", nullable: false),
                    Refused = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Items = table.Column<int>(type: "int", nullable: false),
                    WrittenByModel = table.Column<bool>(type: "bit", nullable: false),
                    Emailed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantBriefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonthlyLimit = table.Column<int>(type: "int", nullable: true),
                    MorningBriefs = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsage_OrganizationId_UserId_Month",
                table: "AiUsage",
                columns: new[] { "OrganizationId", "UserId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBriefs_OrganizationId_UserId_Day",
                table: "AssistantBriefs",
                columns: new[] { "OrganizationId", "UserId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBriefs_Status_CreatedAtUtc",
                table: "AssistantBriefs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantSettings_OrganizationId",
                table: "AssistantSettings",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsage");

            migrationBuilder.DropTable(
                name: "AssistantBriefs");

            migrationBuilder.DropTable(
                name: "AssistantSettings");

            migrationBuilder.DropColumn(
                name: "MorningBriefEmail",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "MorningBriefOff",
                table: "UserSettings");
        }
    }
}
