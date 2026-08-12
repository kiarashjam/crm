using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// Brings every EXISTING organisation onto the agreed eight-stage lead status
    /// vocabulary. Changing the seed list in OrganizationService only affects
    /// organisations created from now on; without this, established tenants keep
    /// the old labels and the pipeline can no longer resolve a status for them.
    ///
    /// Data only — no schema change — so the model snapshot is deliberately
    /// untouched.
    ///
    /// Scope discipline: only the six labels this application itself used to seed
    /// are retired. A status an organisation added by hand is none of our
    /// business and is left exactly where it is.
    /// </summary>
    public partial class AlignLeadStatusVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @target TABLE (Name nvarchar(256) PRIMARY KEY, Ord int);
INSERT INTO @target (Name, Ord) VALUES
    (N'New', 0), (N'Attempted Contact', 1), (N'Contacted', 2), (N'Connected', 3),
    (N'Contract Pending', 4), (N'Awaiting Signature', 5), (N'Signed', 6),
    (N'Lost / Not Interested', 7);

-- Retired labels, and where the leads wearing them should land.
--   Open        -> New                    (it only ever meant 'untouched')
--   In Progress -> Connected              (both mean 'we have met them')
--   Qualified   -> Contract Pending       (met and interested)
--   Open Deal   -> Signed
--   Unqualified -> Lost / Not Interested  (both terminals collapse into one)
--   Lost        -> Lost / Not Interested
-- 'Connected' is NOT remapped even though its meaning narrows from 'meeting
-- booked' to 'meeting attended'. Demoting live leads would be a visible,
-- unexplained regression for the user; the next pipeline edit re-derives the
-- status from recorded work anyway.
DECLARE @map TABLE (OldName nvarchar(256) PRIMARY KEY, NewName nvarchar(256));
INSERT INTO @map (OldName, NewName) VALUES
    (N'Open', N'New'),
    (N'In Progress', N'Connected'),
    (N'Qualified', N'Contract Pending'),
    (N'Open Deal', N'Signed'),
    (N'Unqualified', N'Lost / Not Interested'),
    (N'Lost', N'Lost / Not Interested');

-- 1. Give every organisation any of the eight it is missing.
INSERT INTO LeadStatuses (Id, OrganizationId, Name, DisplayOrder)
SELECT NEWID(), o.Id, t.Name, t.Ord
FROM Organizations o
CROSS JOIN @target t
WHERE NOT EXISTS (
    SELECT 1 FROM LeadStatuses s
    WHERE s.OrganizationId = o.Id AND s.Name = t.Name);

-- 2. Put the eight in the agreed order, including any that already existed.
UPDATE s SET DisplayOrder = t.Ord
FROM LeadStatuses s
INNER JOIN @target t ON t.Name = s.Name;

-- 3. Move leads off the retired labels, repointing the FK at the new row in
--    their OWN organisation. Leads with no organisation keep a null pointer.
UPDATE l
SET Status = m.NewName,
    LeadStatusId = (
        SELECT TOP 1 s2.Id FROM LeadStatuses s2
        WHERE s2.OrganizationId = l.OrganizationId AND s2.Name = m.NewName)
FROM Leads l
INNER JOIN @map m ON m.OldName = l.Status;

-- 4. Break any remaining pointer at a row about to be deleted; the FK would
--    otherwise block step 5.
UPDATE l SET LeadStatusId = NULL
FROM Leads l
INNER JOIN LeadStatuses s ON s.Id = l.LeadStatusId
INNER JOIN @map m ON m.OldName = s.Name;

-- 5. Retire the old default rows.
DELETE s FROM LeadStatuses s INNER JOIN @map m ON m.OldName = s.Name;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the status LIST is restored. The lead-to-status mapping is not
            // reversible: 'Unqualified' and 'Lost' both collapse into a single
            // label on the way up, so there is no way to tell afterwards which
            // one a given lead started as. Inventing an answer would corrupt
            // real records to satisfy a rollback, so the leads are left as they
            // are and the old labels are simply made available again.
            migrationBuilder.Sql(@"
DECLARE @old TABLE (Name nvarchar(256) PRIMARY KEY, Ord int);
INSERT INTO @old (Name, Ord) VALUES
    (N'New', 0), (N'Open', 1), (N'Attempted Contact', 2), (N'Contacted', 3),
    (N'Connected', 4), (N'In Progress', 5), (N'Qualified', 6),
    (N'Unqualified', 7), (N'Open Deal', 8), (N'Lost', 9);

INSERT INTO LeadStatuses (Id, OrganizationId, Name, DisplayOrder)
SELECT NEWID(), o.Id, t.Name, t.Ord
FROM Organizations o
CROSS JOIN @old t
WHERE NOT EXISTS (
    SELECT 1 FROM LeadStatuses s
    WHERE s.OrganizationId = o.Id AND s.Name = t.Name);
");
        }
    }
}
