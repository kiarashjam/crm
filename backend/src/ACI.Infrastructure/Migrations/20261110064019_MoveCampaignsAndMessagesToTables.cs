using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// Campaigns and the communications log in tables of their own, and the club documents
    /// that held them copied in once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every "campaigns" document becomes a campaign with the same id, and every
    /// "communications" document a member message with the same id, marked logged: nothing
    /// was ever delivered, so nothing is shown as sent. A campaign logged as sent stays
    /// Logged; every other one becomes a draft, its planned time dropped, because the old
    /// screen told staff a planned campaign "will not send itself": nothing they planned
    /// then is sent unless someone sends or schedules it again. Moved campaigns are marked
    /// marketing, so they reach only members who agree to it.
    /// </para>
    /// <para>
    /// The documents stay where they are, untouched, until a later release removes them; a
    /// document whose payload is not a JSON object, or that belongs to no organization, is
    /// left there alone. Each insert skips what is already there, so running it again adds
    /// nothing; on a new database there is nothing to copy.
    /// </para>
    /// </remarks>
    public partial class MoveCampaignsAndMessagesToTables : Migration
    {
        // The categories the club screens offer; anything else read from a document is Other.
        private const string Categories = "N'Welcome', N'Dues', N'Renewal', N'Event', N'Reservation', N'Loyalty', N'Announcement', N'Application', N'Other'";

        // A date as the screens and the server wrote them (ISO 8601, mostly with Z), as UTC; null when it does not read as one.
        private static string Utc(string column) => $"CONVERT(datetime2, SWITCHOFFSET(TRY_CONVERT(datetimeoffset, {column}, 127), '+00:00'))";

        // The payload when it is a JSON object, else null, which OPENJSON reads as no rows: a
        // broken document is left behind rather than failing the whole move.
        private const string JsonObject = "CROSS APPLY (SELECT CASE WHEN ISJSON(d.[Payload]) = 1 AND LEFT(LTRIM(d.[Payload]), 1) = N'{' THEN d.[Payload] END AS [Json]) p";

        /// <summary>The copy, on its own, so a test can run it a second time and see it add nothing.</summary>
        public static readonly string MoveDocumentsSql = $@"
INSERT INTO [Campaigns] ([Id], [OrganizationId], [Name], [Channel], [Category], [Purpose], [Subject], [Body], [Segment],
    [Status], [ScheduledAtUtc], [SendingStartedAtUtc], [SentAtUtc], [SentByUserId], [ClaimedUntilUtc], [RecipientCount],
    [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [Version])
SELECT d.[Id], d.[OrganizationId],
    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(j.[name])), N''), N'Campaign'), 200),
    CASE j.[channel] WHEN N'SMS' THEN N'SMS' WHEN N'Push' THEN N'Push' ELSE N'Email' END,
    CASE WHEN j.[category] IN ({Categories}) THEN j.[category] ELSE N'Other' END,
    N'marketing',
    LEFT(COALESCE(j.[subject], N''), 300),
    COALESCE(j.[body], N''),
    COALESCE(j.[segment], N'{{}}'),
    CASE WHEN j.[status] IN (N'Sent', N'Sending') THEN N'Logged' ELSE N'Draft' END,
    NULL, NULL,
    CASE WHEN j.[status] IN (N'Sent', N'Sending') THEN {Utc("j.[sentAtUtc]")} END,
    NULL, NULL,
    CASE WHEN j.[status] IN (N'Sent', N'Sending') THEN COALESCE(TRY_CONVERT(int, j.[recipientCount]), 0) ELSE 0 END,
    COALESCE({Utc("j.[createdAtUtc]")}, d.[UpdatedAtUtc]),
    NULL, d.[UpdatedAtUtc], NEWID()
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [name] nvarchar(max) '$.name', [channel] nvarchar(max) '$.channel', [category] nvarchar(max) '$.category',
    [subject] nvarchar(max) '$.subject', [body] nvarchar(max) '$.body', [segment] nvarchar(max) '$.segment' AS JSON,
    [status] nvarchar(max) '$.status', [sentAtUtc] nvarchar(max) '$.sentAtUtc', [createdAtUtc] nvarchar(max) '$.createdAtUtc',
    [recipientCount] nvarchar(max) '$.recipientCount') j
WHERE d.[Kind] = N'campaigns' AND d.[OrganizationId] IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [Campaigns] c WHERE c.[Id] = d.[Id]);

INSERT INTO [MemberMessages] ([Id], [OrganizationId], [MemberId], [MemberName], [Email], [CampaignId], [CampaignName], [Channel],
    [Category], [Purpose], [Subject], [Preview], [Status], [Reason], [OutboundMessageId], [CreatedByUserId], [CreatedAtUtc])
SELECT d.[Id], d.[OrganizationId], TRY_CONVERT(uniqueidentifier, j.[memberId]),
    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(j.[memberName])), N''), N'Member'), 200),
    NULL, NULL,
    LEFT(NULLIF(LTRIM(RTRIM(j.[campaignName])), N''), 200),
    CASE j.[channel] WHEN N'SMS' THEN N'SMS' WHEN N'Push' THEN N'Push' ELSE N'Email' END,
    CASE WHEN j.[category] IN ({Categories}) THEN j.[category] ELSE N'Other' END,
    NULL,
    LEFT(COALESCE(j.[subject], N''), 300),
    LEFT(j.[preview], 500),
    N'logged',
    NULL, NULL, NULL,
    COALESCE({Utc("j.[sentAtUtc]")}, d.[UpdatedAtUtc])
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [memberId] nvarchar(max) '$.memberId', [memberName] nvarchar(max) '$.memberName', [channel] nvarchar(max) '$.channel',
    [category] nvarchar(max) '$.category', [subject] nvarchar(max) '$.subject', [preview] nvarchar(max) '$.preview',
    [campaignName] nvarchar(max) '$.campaignName', [sentAtUtc] nvarchar(max) '$.sentAtUtc') j
WHERE d.[Kind] = N'communications' AND d.[OrganizationId] IS NOT NULL
    AND TRY_CONVERT(uniqueidentifier, j.[memberId]) IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [MemberMessages] m WHERE m.[Id] = d.[Id]);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SendingStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Preview = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_OrganizationId_CreatedAtUtc",
                table: "Campaigns",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Status_ScheduledAtUtc",
                table: "Campaigns",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberMessages_CampaignId_MemberId",
                table: "MemberMessages",
                columns: new[] { "CampaignId", "MemberId" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberMessages_OrganizationId_CreatedAtUtc",
                table: "MemberMessages",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberMessages_OrganizationId_MemberId_CreatedAtUtc",
                table: "MemberMessages",
                columns: new[] { "OrganizationId", "MemberId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberMessages_OutboundMessageId",
                table: "MemberMessages",
                column: "OutboundMessageId");

            migrationBuilder.Sql(MoveDocumentsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "MemberMessages");
        }
    }
}
