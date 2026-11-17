using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractSignedCopy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SignedCopyAttachmentId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignedCopyAttachmentId",
                table: "Contracts");
        }
    }
}
