using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientHouseAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ClubHouseAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "ClubHouseAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseAccounts_OrganizationId_CompanyId",
                table: "ClubHouseAccounts",
                columns: new[] { "OrganizationId", "CompanyId" },
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubHouseAccounts_OrganizationId_ContactId",
                table: "ClubHouseAccounts",
                columns: new[] { "OrganizationId", "ContactId" },
                filter: "[ContactId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClubHouseAccounts_OrganizationId_CompanyId",
                table: "ClubHouseAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ClubHouseAccounts_OrganizationId_ContactId",
                table: "ClubHouseAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ClubHouseAccounts");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "ClubHouseAccounts");
        }
    }
}
