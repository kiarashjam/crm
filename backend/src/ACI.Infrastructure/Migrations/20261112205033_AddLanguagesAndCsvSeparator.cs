using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguagesAndCsvSeparator : Migration
    {
        /// <summary>
        /// Copies the German and French chosen in the old Profile language select to the account's
        /// language. That select wrote UserSettings.Language, "en" for everyone by default, and
        /// nothing ever read it; a "de" or "fr" there was picked by someone, while "en" cannot be
        /// told from the default and is left as no choice. Copied, not moved: UserSettings keeps
        /// its column. Only accounts without a language are set, so a second run changes nothing.
        /// </summary>
        public const string CopyChosenLanguagesSql = @"
UPDATE u SET u.[Language] = LOWER(LTRIM(RTRIM(s.[Language])))
FROM [Users] u
INNER JOIN [UserSettings] s ON s.[UserId] = u.[Id]
WHERE u.[Language] IS NULL
  AND LOWER(LTRIM(RTRIM(s.[Language]))) IN (N'de', N'fr');";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Users",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CsvSemicolon",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "ClubMembers",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ClubApplications",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.Sql(CopyChosenLanguagesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CsvSemicolon",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "ClubMembers");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "ClubApplications");
        }
    }
}
