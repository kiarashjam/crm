using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KeepEachSubscriptionsRenewalApart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClubRenewals_OrganizationId_MemberId_Source_EndsAtUtc",
                table: "ClubRenewals");

            migrationBuilder.CreateIndex(
                name: "IX_ClubRenewals_OrganizationId_MemberId_Source_SubscriptionId_EndsAtUtc",
                table: "ClubRenewals",
                columns: new[] { "OrganizationId", "MemberId", "Source", "SubscriptionId", "EndsAtUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClubRenewals_OrganizationId_MemberId_Source_SubscriptionId_EndsAtUtc",
                table: "ClubRenewals");

            migrationBuilder.CreateIndex(
                name: "IX_ClubRenewals_OrganizationId_MemberId_Source_EndsAtUtc",
                table: "ClubRenewals",
                columns: new[] { "OrganizationId", "MemberId", "Source", "EndsAtUtc" },
                unique: true);
        }
    }
}
