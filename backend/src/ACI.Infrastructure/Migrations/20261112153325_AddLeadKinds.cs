using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LeadKinds",
                table: "Organizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "person");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine",
                table: "Leads",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Leads",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonEmail",
                table: "Leads",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonName",
                table: "Leads",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonPhone",
                table: "Leads",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonRole",
                table: "Leads",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstablishmentType",
                table: "Leads",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Leads",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "person");

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Leads",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Seats",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Leads",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeadKinds",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AddressLine",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ContactPersonEmail",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ContactPersonName",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ContactPersonPhone",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ContactPersonRole",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "EstablishmentType",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Seats",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Leads");
        }
    }
}
