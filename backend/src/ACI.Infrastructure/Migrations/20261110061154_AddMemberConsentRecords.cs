using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberConsentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_Address",
                table: "ConsentRecords",
                columns: new[] { "OrganizationId", "Address" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_Current",
                table: "ConsentRecords",
                columns: new[] { "OrganizationId", "SubjectType", "SubjectId", "Channel", "Purpose" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_Subject",
                table: "ConsentRecords",
                columns: new[] { "OrganizationId", "SubjectType", "SubjectId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords");
        }
    }
}
