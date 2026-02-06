using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateOrganizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add missing Template columns
            migrationBuilder.AddColumn<string>(
                name: "BrandTone",
                table: "Templates",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "Templates",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSharedWithOrganization",
                table: "Templates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemTemplate",
                table: "Templates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Length",
                table: "Templates",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Templates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Templates",
                type: "datetime2",
                nullable: true);

            // Add indexes
            migrationBuilder.CreateIndex(
                name: "IX_Templates_IsSystemTemplate",
                table: "Templates",
                column: "IsSystemTemplate");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_OrganizationId",
                table: "Templates",
                column: "OrganizationId");

            // Add foreign key
            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Organizations_OrganizationId",
                table: "Templates",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Organizations_OrganizationId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Templates_IsSystemTemplate",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Templates_OrganizationId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "BrandTone",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "IsSharedWithOrganization",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "IsSystemTemplate",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "Length",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Templates");
        }
    }
}
