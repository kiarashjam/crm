using ACI.Domain.Common;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// Brings every EXISTING organisation onto the agreed eight-stage lead status
    /// vocabulary. Changing the seed list only affects organisations created from
    /// then on; without this, established tenants keep the old labels and the
    /// pipeline can no longer resolve a status for them.
    ///
    /// IMPORTANT: this migration has no .Designer.cs, so it carries no [Migration]
    /// attribute and EF's assembly scan does not discover it — MigrateAsync has
    /// never run it and still does not. That is why the alignment is applied from
    /// Program.cs alongside this application's other hand-written DDL, which is
    /// where its schema work actually happens. This class is kept, and generates
    /// its SQL from the same LeadStatusVocabulary, so that if the migration chain
    /// is ever regenerated the two cannot disagree.
    ///
    /// Data only — no schema change — so the model snapshot is deliberately
    /// untouched.
    /// </summary>
    public partial class AlignLeadStatusVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(LeadStatusVocabulary.AlignmentSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the status LIST is restored. The lead-to-status mapping is not
            // reversible: 'Unqualified' and 'Lost' both collapse into a single
            // label on the way up, so there is no way to tell afterwards which one
            // a given lead started as. Inventing an answer would corrupt real
            // records to satisfy a rollback, so the leads are left as they are and
            // the old labels are simply made available again.
            migrationBuilder.Sql(@"
DECLARE @old TABLE (Name nvarchar(128) PRIMARY KEY, Ord int);
INSERT INTO @old (Name, Ord) VALUES
    (N'New', 0), (N'Open', 1), (N'Attempted Contact', 2), (N'Contacted', 3),
    (N'Connected', 4), (N'In Progress', 5), (N'Qualified', 6),
    (N'Unqualified', 7), (N'Open Deal', 8), (N'Lost', 9);

INSERT INTO [LeadStatuses] ([Id], [OrganizationId], [Name], [DisplayOrder])
SELECT NEWID(), o.[Id], t.Name, t.Ord
FROM [Organizations] o
CROSS JOIN @old t
WHERE NOT EXISTS (
    SELECT 1 FROM [LeadStatuses] s
    WHERE s.[OrganizationId] = o.[Id] AND s.[Name] = t.Name);
");
        }
    }
}
