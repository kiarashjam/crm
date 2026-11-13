using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlexibleData : Migration
    {
        /// <summary>
        /// Copies every custom field value kept the old way, as one JSON object per record
        /// (CustomFieldValues), into a typed row of CustomFieldEntries; public so a test can
        /// run it a second time and see it add nothing.
        /// </summary>
        /// <remarks>
        /// A value is copied only where its key still names a field of the record's kind in the
        /// record's workspace (in a personal workspace, the same person's), and only into the
        /// column that field's type is kept in: text, a web address and an option as text (its
        /// first 200 characters again as the key lists filter and sort by), a number, a date
        /// written yyyy-MM-dd, ticked or not. A value that does not read as its type, an empty
        /// one, and the keys of fields deleted since stay where they were and are not copied:
        /// the old way never showed them either. A record and field that already have a row are
        /// left alone, so running this again adds nothing. The old rows are kept, read-only,
        /// until a later release; nothing reads them any more.
        /// OPENJSON names its keys in a binary collation, so a field's key is compared in that
        /// one too: exactly, as the old code looked a key up in the JSON, and without the
        /// collation conflict comparing it to a column of the database's default would raise.
        /// </remarks>
        public const string CopyCustomFieldValuesSql = @"
WITH [Values] AS (
    SELECT s.[OrganizationId], s.[OwnerUserId], s.[EntityType], s.[RecordId], s.[UpdatedAtUtc],
        d.[Id] AS [FieldId], d.[Type], j.[value] AS [Raw], j.[type] AS [JsonType],
        ROW_NUMBER() OVER (PARTITION BY s.[RecordId], d.[Id] ORDER BY s.[UpdatedAtUtc] DESC) AS [Nth]
    FROM [CustomFieldValues] s
    CROSS APPLY OPENJSON(CASE WHEN ISJSON(s.[ValuesJson]) = 1 THEN s.[ValuesJson] END) j
    JOIN [CustomFieldDefinitions] d
        ON d.[EntityType] = s.[EntityType] AND d.[Key] COLLATE Latin1_General_BIN2 = j.[key]
       AND ((s.[OrganizationId] IS NOT NULL AND d.[OrganizationId] = s.[OrganizationId])
         OR (s.[OrganizationId] IS NULL AND d.[OrganizationId] IS NULL AND d.[OwnerUserId] = s.[OwnerUserId]))
), [Typed] AS (
    SELECT v.*,
        CASE WHEN v.[Type] IN (N'text', N'url', N'select') AND v.[JsonType] = 1 AND LTRIM(RTRIM(v.[Raw])) <> N''
            THEN LEFT(LTRIM(RTRIM(v.[Raw])), 2000) END AS [TextValue],
        CASE WHEN v.[Type] = N'number' AND v.[JsonType] IN (1, 2) THEN TRY_CONVERT(decimal(28, 8), LTRIM(RTRIM(v.[Raw]))) END AS [NumberValue],
        CASE WHEN v.[Type] = N'date' AND v.[JsonType] = 1 AND LEN(LTRIM(RTRIM(v.[Raw]))) = 10
            THEN TRY_CONVERT(date, LTRIM(RTRIM(v.[Raw])), 23) END AS [DateValue],
        CASE WHEN v.[Type] = N'checkbox' AND v.[JsonType] = 3
            THEN CASE v.[Raw] WHEN N'true' THEN CAST(1 AS bit) WHEN N'false' THEN CAST(0 AS bit) END END AS [BoolValue]
    FROM [Values] v
    WHERE v.[Nth] = 1
)
INSERT INTO [CustomFieldEntries]
    ([Id], [OrganizationId], [OwnerUserId], [EntityType], [RecordId], [FieldId],
     [TextValue], [TextKey], [NumberValue], [Currency], [DateValue], [BoolValue], [LinkId], [UpdatedAtUtc])
SELECT NEWID(), t.[OrganizationId], t.[OwnerUserId], t.[EntityType], t.[RecordId], t.[FieldId],
    t.[TextValue], LEFT(t.[TextValue], 200), t.[NumberValue], NULL, t.[DateValue], t.[BoolValue], NULL, t.[UpdatedAtUtc]
FROM [Typed] t
WHERE (t.[TextValue] IS NOT NULL OR t.[NumberValue] IS NOT NULL OR t.[DateValue] IS NOT NULL OR t.[BoolValue] IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM [CustomFieldEntries] e WHERE e.[RecordId] = t.[RecordId] AND e.[FieldId] = t.[FieldId]);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "CustomFieldDefinitions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultValueJson",
                table: "CustomFieldDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkEntityType",
                table: "CustomFieldDefinitions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomFieldEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TextValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TextKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberValue = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DateValue = table.Column<DateOnly>(type: "date", nullable: true),
                    BoolValue = table.Column<bool>(type: "bit", nullable: true),
                    LinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldEntries_CustomFieldDefinitions_FieldId",
                        column: x => x.FieldId,
                        principalTable: "CustomFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    List = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Shared = table.Column<bool>(type: "bit", nullable: false),
                    QueryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViewDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    List = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewDefaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViewDefaults_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecordTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldEntries_FieldId",
                table: "CustomFieldEntries",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldEntries_OrganizationId_FieldId_DateValue",
                table: "CustomFieldEntries",
                columns: new[] { "OrganizationId", "FieldId", "DateValue" })
                .Annotation("SqlServer:Include", new[] { "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldEntries_OrganizationId_FieldId_LinkId",
                table: "CustomFieldEntries",
                columns: new[] { "OrganizationId", "FieldId", "LinkId" })
                .Annotation("SqlServer:Include", new[] { "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldEntries_OrganizationId_FieldId_NumberValue",
                table: "CustomFieldEntries",
                columns: new[] { "OrganizationId", "FieldId", "NumberValue" })
                .Annotation("SqlServer:Include", new[] { "RecordId", "BoolValue" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldEntries_OrganizationId_FieldId_TextKey",
                table: "CustomFieldEntries",
                columns: new[] { "OrganizationId", "FieldId", "TextKey" })
                .Annotation("SqlServer:Include", new[] { "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldEntries_RecordId_FieldId_TextKey",
                table: "CustomFieldEntries",
                columns: new[] { "RecordId", "FieldId", "TextKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordTags_OrganizationId_EntityType",
                table: "RecordTags",
                columns: new[] { "OrganizationId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordTags_RecordId_TagId",
                table: "RecordTags",
                columns: new[] { "RecordId", "TagId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordTags_TagId_RecordId",
                table: "RecordTags",
                columns: new[] { "TagId", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewDefaults_SavedViewId",
                table: "SavedViewDefaults",
                column: "SavedViewId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewDefaults_UserId_OrganizationId_List",
                table: "SavedViewDefaults",
                columns: new[] { "UserId", "OrganizationId", "List" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_OrganizationId_List",
                table: "SavedViews",
                columns: new[] { "OrganizationId", "List" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OrganizationId_Name",
                table: "Tags",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.Sql(CopyCustomFieldValuesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldEntries");

            migrationBuilder.DropTable(
                name: "RecordTags");

            migrationBuilder.DropTable(
                name: "SavedViewDefaults");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultValueJson",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "LinkEntityType",
                table: "CustomFieldDefinitions");
        }
    }
}
