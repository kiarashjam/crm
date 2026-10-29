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
    /// What OrganizationService seeded until the August 2026 rework. Only used to tell
    /// an organisation still on that list from one already on <see cref="Default"/>.
    /// </summary>
    public static readonly string[] PreviousDefault =
    {
        "New", "Open", "Attempted Contact", "Contacted", "Connected",
        "In Progress", "Qualified", "Unqualified", "Open Deal", "Lost",
    };

    /// <summary>
    /// The stages <see cref="Default"/> added. Every organisation made since the rework
    /// is seeded with them, and every one before it was given them on a later start, so
    /// an organisation with none of them has never been brought onto the agreed list.
    /// </summary>
    public static string[] AddedInRework => Default.Except(PreviousDefault).ToArray();

    /// <summary>The stage that means "they said no". Last in the list, but not "furthest along".</summary>
    public const string Lost = "Lost / Not Interested";

    /// <summary>
    /// Where a status sits in <see cref="Default"/>, reading a retired label as the
    /// stage it was mapped to. -1 for a status this vocabulary does not know.
    /// </summary>
    public static int StageIndex(string? status)
    {
        var name = (status ?? "").Trim();
        foreach (var (old, @new) in Retired)
        {
            if (string.Equals(name, old, StringComparison.OrdinalIgnoreCase)) { name = @new; break; }
        }
        return Array.FindIndex(Default, stage => string.Equals(stage, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True when <paramref name="status"/> is <paramref name="stage"/> or further
    /// along — never for a lost lead, which sits last in the list without being
    /// furthest along.
    /// </summary>
    public static bool ReachedStage(string? status, string stage)
    {
        var at = StageIndex(status);
        var target = StageIndex(stage);
        if (at < 0 || target < 0 || at == StageIndex(Lost)) return false;
        return at >= target;
    }

    /// <summary>
    /// T-SQL that brings the organisations still on <see cref="PreviousDefault"/> onto
    /// <see cref="Default"/>. Startup runs it once, through the AppliedDataFixes ledger.
    /// </summary>
    /// <remarks>
    /// It used to run on every start, over every organisation, and that undid managers'
    /// work: it put back stages they had deleted and the order they had changed, and it
    /// deleted any status they made with a retired name ("Qualified" is the example in
    /// the API's own docs), moving its leads elsewhere with no history of why. Now it
    /// touches only organisations with none of <see cref="AddedInRework"/>, whose
    /// statuses are still the ones this application seeded. Their leads' moves are
    /// written to the status history, and a retired row is deleted only when no lead
    /// points at it or wears its name.
    /// </remarks>
    public static string AlignmentSql()
    {
        var target = string.Join(",\n    ", Default.Select((name, i) => $"({Quote(name)}, {i})"));
        var map = string.Join(",\n    ", Retired.Select(r => $"({Quote(r.Old)}, {Quote(r.New)})"));
        var added = string.Join(",\n    ", AddedInRework.Select(name => $"({Quote(name)})"));

        return $@"
DECLARE @target TABLE (Name nvarchar(128) PRIMARY KEY, Ord int);
INSERT INTO @target (Name, Ord) VALUES
    {target};

DECLARE @map TABLE (OldName nvarchar(128) PRIMARY KEY, NewName nvarchar(128));
INSERT INTO @map (OldName, NewName) VALUES
    {map};

DECLARE @added TABLE (Name nvarchar(128) PRIMARY KEY);
INSERT INTO @added (Name) VALUES
    {added};

-- 1. The organisations still on the old list: none of the stages the agreed list
--    added. Everywhere else the statuses are the organisation's own, and are left
--    exactly as its managers made them.
DECLARE @orgs TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @orgs (Id)
SELECT o.[Id] FROM [Organizations] o
WHERE NOT EXISTS (
    SELECT 1 FROM [LeadStatuses] s INNER JOIN @added a ON a.Name = s.[Name]
    WHERE s.[OrganizationId] = o.[Id]);

-- 2. Give them the agreed stages they are missing.
INSERT INTO [LeadStatuses] ([Id], [OrganizationId], [Name], [DisplayOrder])
SELECT NEWID(), o.Id, t.Name, t.Ord
FROM @orgs o
CROSS JOIN @target t
WHERE NOT EXISTS (
    SELECT 1 FROM [LeadStatuses] s
    WHERE s.[OrganizationId] = o.Id AND s.[Name] = t.Name);

-- 3. Put the agreed stages in the agreed order.
UPDATE s SET [DisplayOrder] = t.Ord
FROM [LeadStatuses] s
INNER JOIN @orgs o ON o.Id = s.[OrganizationId]
INNER JOIN @target t ON t.Name = s.[Name]
WHERE s.[DisplayOrder] <> t.Ord;

-- 4. Move their leads off the retired labels, onto the new row in the same
--    organisation, and record each move in the lead's status history.
INSERT INTO [LeadStatusEvents] ([Id], [LeadId], [OrganizationId], [FromStatus], [ToStatus], [ChangedByUserId], [ChangedAtUtc])
SELECT NEWID(), l.[Id], l.[OrganizationId], l.[Status], m.NewName, NULL, SYSUTCDATETIME()
FROM [Leads] l
INNER JOIN @orgs o ON o.Id = l.[OrganizationId]
INNER JOIN @map m ON m.OldName = l.[Status];

UPDATE l
SET [Status] = m.NewName,
    [LeadStatusId] = (
        SELECT TOP 1 s2.[Id] FROM [LeadStatuses] s2
        WHERE s2.[OrganizationId] = l.[OrganizationId] AND s2.[Name] = m.NewName)
FROM [Leads] l
INNER JOIN @orgs o ON o.Id = l.[OrganizationId]
INNER JOIN @map m ON m.OldName = l.[Status];

-- 5. Retire the old rows nothing uses any more. A row a lead still points at, or
--    whose name a lead still wears, stays.
DELETE s
FROM [LeadStatuses] s
INNER JOIN @orgs o ON o.Id = s.[OrganizationId]
INNER JOIN @map m ON m.OldName = s.[Name]
WHERE NOT EXISTS (SELECT 1 FROM [Leads] l WHERE l.[LeadStatusId] = s.[Id])
  AND NOT EXISTS (SELECT 1 FROM [Leads] l WHERE l.[OrganizationId] = s.[OrganizationId] AND l.[Status] = s.[Name]);
";
    }

    /// <summary>
    /// A T-SQL string literal. Every name in this file is ours and ASCII, but the
    /// SQL is built by concatenation, so the quoting is done properly rather than
    /// relying on that staying true.
    /// </summary>
    private static string Quote(string value) => "N'" + value.Replace("'", "''") + "'";
}
