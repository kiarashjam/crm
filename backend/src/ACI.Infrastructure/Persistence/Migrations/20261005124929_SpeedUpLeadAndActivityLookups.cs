using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpeedUpLeadAndActivityLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These columns are part of the current model but were missing from the
            // migration history. Add them only when the live database does not have them.
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Leads', 'AssignedToUserId') IS NULL
                    ALTER TABLE [Leads] ADD [AssignedToUserId] uniqueidentifier NULL;

                IF COL_LENGTH('dbo.Leads', 'PipelineState') IS NULL
                    ALTER TABLE [Leads] ADD [PipelineState] nvarchar(max) NULL;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Leads_AssignedToUserId' AND object_id = OBJECT_ID('dbo.Leads'))
                    CREATE INDEX [IX_Leads_AssignedToUserId] ON [Leads] ([AssignedToUserId]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Leads_OrganizationId_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.Leads'))
                    CREATE INDEX [IX_Leads_OrganizationId_CreatedAtUtc] ON [Leads] ([OrganizationId], [CreatedAtUtc]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Leads_OrganizationId_Status' AND object_id = OBJECT_ID('dbo.Leads'))
                    CREATE INDEX [IX_Leads_OrganizationId_Status] ON [Leads] ([OrganizationId], [Status]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Activities_OrganizationId_LeadId' AND object_id = OBJECT_ID('dbo.Activities'))
                    CREATE INDEX [IX_Activities_OrganizationId_LeadId] ON [Activities] ([OrganizationId], [LeadId]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leads_Users_AssignedToUserId')
                    ALTER TABLE [Leads] ADD CONSTRAINT [FK_Leads_Users_AssignedToUserId]
                        FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Leads_OrganizationId_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.Leads'))
                    DROP INDEX [IX_Leads_OrganizationId_CreatedAtUtc] ON [Leads];

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Leads_OrganizationId_Status' AND object_id = OBJECT_ID('dbo.Leads'))
                    DROP INDEX [IX_Leads_OrganizationId_Status] ON [Leads];

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Activities_OrganizationId_LeadId' AND object_id = OBJECT_ID('dbo.Activities'))
                    DROP INDEX [IX_Activities_OrganizationId_LeadId] ON [Activities];
                """);
        }
    }
}
