using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteAnalyticsDigests",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Recipients = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteAnalyticsDigests", x => x.Day);
                });

            migrationBuilder.CreateTable(
                name: "SiteAnalyticsSalts",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    ProtectedSalt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteAnalyticsSalts", x => x.Day);
                });

            migrationBuilder.CreateTable(
                name: "SiteBotHits",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Bot = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Hits = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteBotHits", x => new { x.Day, x.Bot });
                });

            migrationBuilder.CreateTable(
                name: "SiteEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReferrerHost = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    UtmSource = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    UtmMedium = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    UtmCampaign = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VisitorHash = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    SessionKey = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Device = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Browser = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Os = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    SignedIn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteEvents_Day_SessionKey_OccurredAtUtc",
                table: "SiteEvents",
                columns: new[] { "Day", "SessionKey", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteEvents_Day_Type",
                table: "SiteEvents",
                columns: new[] { "Day", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteEvents_OccurredAtUtc",
                table: "SiteEvents",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteAnalyticsDigests");

            migrationBuilder.DropTable(
                name: "SiteAnalyticsSalts");

            migrationBuilder.DropTable(
                name: "SiteBotHits");

            migrationBuilder.DropTable(
                name: "SiteEvents");
        }
    }
}
