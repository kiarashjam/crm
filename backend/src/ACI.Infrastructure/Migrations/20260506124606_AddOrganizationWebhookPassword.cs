using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationWebhookPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebhookPassword",
                table: "Organizations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebhookPassword",
                table: "Organizations");
        }
    }
}
