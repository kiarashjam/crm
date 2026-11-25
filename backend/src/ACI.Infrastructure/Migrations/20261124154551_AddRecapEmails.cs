using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecapEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecapSends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecapSends", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecapSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailySinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WeeklySinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MonthlySinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecapSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecapSends_OrganizationId_UserId_Frequency_PeriodStart",
                table: "RecapSends",
                columns: new[] { "OrganizationId", "UserId", "Frequency", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecapSends_Status_CreatedAtUtc",
                table: "RecapSends",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecapSubscriptions_OrganizationId_UserId",
                table: "RecapSubscriptions",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecapSubscriptions_UserId",
                table: "RecapSubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecapSends");

            migrationBuilder.DropTable(
                name: "RecapSubscriptions");
        }
    }
}
