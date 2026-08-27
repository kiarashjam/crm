namespace ACI.Domain.Common;

/// <summary>
/// The one place the lead status vocabulary is written down.
///
/// It used to be written twice — once as a seed list in OrganizationService (which
/// only ever affects organisations created from that point on) and once in a
/// migration meant to bring existing organisations onto the same list. The
/// migration was never discovered by EF, so the two drifted: new organisations got
/// the eight stages below while every established one kept the old six, and the
/// pipeline could not resolve a status for them. Leads sat on "New" no matter how
/// much work was logged against them.
///
/// So both callers now read from here, and <see cref="AlignmentSql"/> is generated
/// from the same arrays rather than restating them.
/// </summary>
public static class LeadStatusVocabulary
{
    /// <summary>
    /// The agreed stages, in display order. These mirror the five-phase lead
    /// pipeline one-for-one so a status is always derivable from recorded work
    /// rather than typed in separately — see leadStatusSync.ts for the mapping.
    /// </summary>
    public static readonly string[] Default =
    {
        "New",
        "Attempted Contact",
        "Contacted",
        "Connected",
        "Contract Pending",
        "Awaiting Signature",
        "Signed",
        "Lost / Not Interested",
    };

    /// <summary>
    /// Labels this application used to seed, and where the leads wearing them
    /// should land.
    ///
    /// Scope discipline: only labels this application itself seeded are retired. A
    /// status an organisation added by hand is none of our business and is left
    /// exactly where it is.
    ///
    /// "Connected" is deliberately absent even though its meaning narrows from
    /// "meeting booked" to "meeting attended". Demoting live leads would be a
    /// visible, unexplained regression; the next pipeline edit re-derives the
    /// status from recorded work anyway.
    /// </summary>
    public static readonly (string Old, string New)[] Retired =
    {
        ("Open", "New"),                            // only ever meant 'untouched'
        ("In Progress", "Connected"),               // both mean 'we have met them'
        ("Qualified", "Contract Pending"),          // met and interested
        ("Open Deal", "Signed"),
        ("Unqualified", "Lost / Not Interested"),   // both terminals collapse into one
        ("Lost", "Lost / Not Interested"),
    };

    /// <summary>
    /// Idempotent T-SQL that brings every organisation in the database onto
    /// <see cref="Default"/>. Safe to run on every boot: each statement converges,
    /// so a second run over an already-aligned database changes nothing.
    /// </summary>
    public static string AlignmentSql()
    {
        var target = string.Join(",\n    ", Default.Select((name, i) => $"({Quote(name)}, {i})"));
        var map = string.Join(",\n    ", Retired.Select(r => $"({Quote(r.Old)}, {Quote(r.New)})"));

        return $@"
DECLARE @target TABLE (Name nvarchar(128) PRIMARY KEY, Ord int);
INSERT INTO @target (Name, Ord) VALUES
    {target};

DECLARE @map TABLE (OldName nvarchar(128) PRIMARY KEY, NewName nvarchar(128));
INSERT INTO @map (OldName, NewName) VALUES
    {map};

-- 1. Give every organisation any of the agreed stages it is missing.
INSERT INTO [LeadStatuses] ([Id], [OrganizationId], [Name], [DisplayOrder])
SELECT NEWID(), o.[Id], t.Name, t.Ord
FROM [Organizations] o
CROSS JOIN @target t
WHERE NOT EXISTS (
    SELECT 1 FROM [LeadStatuses] s
    WHERE s.[OrganizationId] = o.[Id] AND s.[Name] = t.Name);

-- 2. Put them in the agreed order, including any that already existed.
UPDATE s SET [DisplayOrder] = t.Ord
FROM [LeadStatuses] s
INNER JOIN @target t ON t.Name = s.[Name]
WHERE s.[DisplayOrder] <> t.Ord;

-- 3. Move leads off the retired labels, repointing the FK at the new row in
--    their OWN organisation. Leads with no organisation keep a null pointer.
UPDATE l
SET [Status] = m.NewName,
    [LeadStatusId] = (
        SELECT TOP 1 s2.[Id] FROM [LeadStatuses] s2
        WHERE s2.[OrganizationId] = l.[OrganizationId] AND s2.[Name] = m.NewName)
FROM [Leads] l
INNER JOIN @map m ON m.OldName = l.[Status];

-- 4. Break any remaining pointer at a row about to be deleted; the FK would
--    otherwise block step 5.
UPDATE l SET [LeadStatusId] = NULL
FROM [Leads] l
INNER JOIN [LeadStatuses] s ON s.[Id] = l.[LeadStatusId]
INNER JOIN @map m ON m.OldName = s.[Name];

-- 5. Retire the old default rows.
DELETE s FROM [LeadStatuses] s INNER JOIN @map m ON m.OldName = s.[Name];
";
    }

    /// <summary>
    /// A T-SQL string literal. Every name in this file is ours and ASCII, but the
    /// SQL is built by concatenation, so the quoting is done properly rather than
    /// relying on that staying true.
    /// </summary>
    private static string Quote(string value) => "N'" + value.Replace("'", "''") + "'";
}
