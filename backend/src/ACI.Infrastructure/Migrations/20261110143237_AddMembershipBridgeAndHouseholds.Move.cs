namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// What this migration copies into the tables it makes: the club's application and
    /// household documents, the couples recorded between contacts, and each member's contact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the migration rather than a start-up data fix, so the tables and what is copied into
    /// them are one transaction, run once per database by the migration history: a copy that
    /// fails leaves no tables behind rather than an Applications page that reads as empty. It
    /// copies: the documents and the contact relationships stay as they are. Every application
    /// and household keeps its document's id, a person is linked once, and each step only adds
    /// what is not there yet, so running it again adds nothing. A document whose payload is not
    /// a JSON object, or that belongs to no organization (the old personal bucket), stays behind.
    /// </para>
    /// <para>
    /// Members find their contact. A member is linked to the workspace's one active contact with
    /// their email when that contact also has their name (case and accents aside), and to the
    /// lead that contact came from. Two people sharing a mailbox are not linked to each other's
    /// contact: the name has to match as well.
    /// </para>
    /// <para>
    /// Applications come over as they were. One the old flow approved finds the member it made
    /// (the one member with that email and name) and is marked as approved by that flow, so no
    /// dues or fee are ever posted for it again. Each finds its contact and lead the same way a
    /// member does.
    /// </para>
    /// <para>
    /// Households and their links come over with each member once: someone linked into two
    /// households stays in the first. The primary is the member the household names (or else the
    /// first link marked Primary), one per household. Roll-up billing starts off for every link:
    /// the page said it was not applied, so turning it on is left to staff, rather than the next
    /// bar tab of every spouse moving to someone else's account the day this release goes out.
    /// </para>
    /// <para>
    /// Couples move into households, because a spouse or partner is the one family tie both models
    /// recorded. For each spouse or partner relationship between two active contacts, oldest
    /// first: when neither of them is in a household (as a contact, or through their member), a
    /// household is made with the contact it was recorded on as its primary; when one of them is
    /// the primary of a household with no couple yet, the other joins it. Otherwise the
    /// relationship stays as it was. A person who is a member is linked as the member.
    /// </para>
    /// <para>
    /// Frozen with the migration on purpose: it reads the documents as they were written up to
    /// this release, and nothing later should change what it did.
    /// </para>
    /// </remarks>
    public partial class AddMembershipBridgeAndHouseholds
    {
        // A date as the screens and the server wrote them (ISO 8601, mostly with Z), as UTC; null when it does not read as one.
        private static string Utc(string column) => $"CONVERT(datetime2, SWITCHOFFSET(TRY_CONVERT(datetimeoffset, {column}, 127), '+00:00'))";

        private static string Text(string column, int max) => $"LEFT(NULLIF(LTRIM(RTRIM({column})), N''), {max})";

        // An address as two records are compared on it.
        private static string Address(string column) => $"LOWER(LTRIM(RTRIM(COALESCE({column}, N''))))";

        // Two names that are one person's: case, accents and the spaces around them aside.
        private static string SameName(string a, string b) =>
            $"LTRIM(RTRIM({a})) COLLATE Latin1_General_CI_AI = LTRIM(RTRIM({b})) COLLATE Latin1_General_CI_AI";

        // The payload when it is a JSON object, else null, which OPENJSON reads as no rows.
        private const string JsonObject =
            "CROSS APPLY (SELECT CASE WHEN ISJSON(d.[Payload]) = 1 AND LEFT(LTRIM(d.[Payload]), 1) = N'{' THEN d.[Payload] END AS [Json]) p";

        private const string ApplicantName = "COALESCE(j.[firstName], N'') + N' ' + COALESCE(j.[lastName], N'')";

        /// <summary>The copy, on its own, so a test can run it a second time and see it add nothing.</summary>
        public static readonly string MoveSql = $@"
SET NOCOUNT ON;

-- ── members find their contact ───────────────────────────────────────────────
UPDATE m
SET m.[ContactId] = c.[Id],
    m.[LeadId] = COALESCE(m.[LeadId], lead.[Id])
FROM [ClubMembers] m
JOIN [Contacts] c ON c.[OrganizationId] = m.[OrganizationId] AND c.[IsArchived] = 0
    AND {Address("c.[Email]")} = {Address("m.[Email]")}
    AND {SameName("c.[Name]", "m.[FirstName] + N' ' + m.[LastName]")}
LEFT JOIN [Leads] lead ON lead.[Id] = c.[ConvertedFromLeadId] AND lead.[OrganizationId] = m.[OrganizationId]
WHERE m.[ContactId] IS NULL AND m.[OrganizationId] IS NOT NULL AND {Address("m.[Email]")} <> N''
    AND (SELECT COUNT(*) FROM [Contacts] c2
         WHERE c2.[OrganizationId] = m.[OrganizationId] AND c2.[IsArchived] = 0 AND {Address("c2.[Email]")} = {Address("m.[Email]")}) = 1;

-- ── applications ─────────────────────────────────────────────────────────────
INSERT INTO [ClubApplications] ([Id], [OrganizationId], [FirstName], [LastName], [Email], [Phone], [RequestedTier], [ReferredByName],
    [Occupation], [ReasonForJoining], [Status], [Source], [SubmittedAtUtc], [ReviewedAtUtc], [ReviewedByUserId], [ReviewerNotes],
    [LeadId], [ContactId], [HouseholdId], [HouseholdRole], [MemberId], [ApprovedAtUtc], [ApprovedByUserId], [ApprovalTrigger],
    [ContractId], [DuesSubscriptionId], [InitiationChargeId], [MoneyPending], [DepositPaymentId], [WelcomeMessageId],
    [ApprovalClaimedUntilUtc], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [Version])
SELECT d.[Id], d.[OrganizationId],
    LEFT(COALESCE({Text("j.[firstName]", 100)}, N''), 100),
    LEFT(COALESCE({Text("j.[lastName]", 100)}, N''), 100),
    LEFT(COALESCE({Text("j.[email]", 256)}, N''), 256),
    {Text("j.[phone]", 64)},
    LEFT(COALESCE({Text("j.[requestedTier]", 32)}, N''), 32),
    {Text("j.[referredByName]", 200)},
    {Text("j.[occupation]", 200)},
    {Text("j.[reasonForJoining]", 2000)},
    st.[Status], N'moved', s.[SubmittedAtUtc],
    {Utc("j.[reviewedAtUtc]")}, NULL,
    {Text("j.[reviewerNotes]", 2000)},
    COALESCE(member.[LeadId], personLead.[Id]), COALESCE(member.[ContactId], person.[ContactId]),
    NULL, NULL,
    CASE WHEN st.[Status] = N'Approved' THEN member.[Id] END,
    CASE WHEN st.[Status] = N'Approved' THEN COALESCE({Utc("j.[reviewedAtUtc]")}, s.[SubmittedAtUtc]) END,
    NULL,
    CASE WHEN st.[Status] = N'Approved' THEN N'moved' END,
    NULL, NULL, NULL, 0, NULL, NULL, NULL,
    s.[SubmittedAtUtc], NULL, d.[UpdatedAtUtc], NEWID()
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [firstName] nvarchar(max) '$.firstName', [lastName] nvarchar(max) '$.lastName', [email] nvarchar(max) '$.email',
    [phone] nvarchar(max) '$.phone', [requestedTier] nvarchar(max) '$.requestedTier', [referredByName] nvarchar(max) '$.referredByName',
    [occupation] nvarchar(max) '$.occupation', [reasonForJoining] nvarchar(max) '$.reasonForJoining', [status] nvarchar(max) '$.status',
    [submittedAtUtc] nvarchar(max) '$.submittedAtUtc', [reviewedAtUtc] nvarchar(max) '$.reviewedAtUtc',
    [reviewerNotes] nvarchar(max) '$.reviewerNotes') j
CROSS APPLY (SELECT CASE WHEN j.[status] IN (N'Submitted', N'UnderReview', N'Approved', N'Rejected', N'Waitlisted')
    THEN j.[status] ELSE N'Submitted' END AS [Status]) st
CROSS APPLY (SELECT COALESCE({Utc("j.[submittedAtUtc]")}, d.[UpdatedAtUtc]) AS [SubmittedAtUtc]) s
-- The member an approval made: the one member of the workspace with this address and name.
OUTER APPLY (
    SELECT x.[Id], x.[ContactId], x.[LeadId] FROM [ClubMembers] x
    WHERE x.[OrganizationId] = d.[OrganizationId] AND {Address("j.[email]")} <> N''
        AND {Address("x.[Email]")} = {Address("j.[email]")}
        AND {SameName("x.[FirstName] + N' ' + x.[LastName]", ApplicantName)}
        AND (SELECT COUNT(*) FROM [ClubMembers] y
             WHERE y.[OrganizationId] = d.[OrganizationId] AND {Address("y.[Email]")} = {Address("j.[email]")}
                AND {SameName("y.[FirstName] + N' ' + y.[LastName]", ApplicantName)}) = 1
) member
-- The applicant's contact: the one active contact with this address, when it has their name.
OUTER APPLY (
    SELECT c.[Id] AS [ContactId], c.[ConvertedFromLeadId] AS [LeadId] FROM [Contacts] c
    WHERE c.[OrganizationId] = d.[OrganizationId] AND c.[IsArchived] = 0 AND {Address("j.[email]")} <> N''
        AND {Address("c.[Email]")} = {Address("j.[email]")}
        AND {SameName("c.[Name]", ApplicantName)}
        AND (SELECT COUNT(*) FROM [Contacts] c2
             WHERE c2.[OrganizationId] = d.[OrganizationId] AND c2.[IsArchived] = 0 AND {Address("c2.[Email]")} = {Address("j.[email]")}) = 1
) person
LEFT JOIN [Leads] personLead ON personLead.[Id] = person.[LeadId] AND personLead.[OrganizationId] = d.[OrganizationId]
WHERE d.[Kind] = N'applications' AND d.[OrganizationId] IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [ClubApplications] x WHERE x.[Id] = d.[Id]);

-- ── households ───────────────────────────────────────────────────────────────
INSERT INTO [ClubHouseholds] ([Id], [OrganizationId], [Name], [Notes], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [Version])
SELECT d.[Id], d.[OrganizationId],
    LEFT(COALESCE({Text("j.[name]", 200)}, N'Household'), 200),
    {Text("j.[notes]", 2000)},
    COALESCE({Utc("j.[createdAtUtc]")}, d.[UpdatedAtUtc]), NULL, d.[UpdatedAtUtc], NEWID()
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [name] nvarchar(max) '$.name', [notes] nvarchar(max) '$.notes', [createdAtUtc] nvarchar(max) '$.createdAtUtc') j
WHERE d.[Kind] = N'households' AND d.[OrganizationId] IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [ClubHouseholds] x WHERE x.[Id] = d.[Id]);

-- ── who is in each household ─────────────────────────────────────────────────
-- Every link a document made, and the primary a household names where no link made them one.
-- WantsPrimary: 2 for the member the household names, 1 for a link marked Primary, else 0.
DECLARE @links TABLE ([Id] uniqueidentifier, [OrganizationId] uniqueidentifier, [HouseholdId] uniqueidentifier,
    [MemberId] uniqueidentifier, [Role] nvarchar(20), [WantsPrimary] int, [At] datetime2, [HouseholdAt] datetime2);

INSERT INTO @links ([Id], [OrganizationId], [HouseholdId], [MemberId], [Role], [WantsPrimary], [At], [HouseholdAt])
SELECT d.[Id], d.[OrganizationId], h.[Id], m.[Id],
    CASE WHEN j.[role] IN (N'Spouse', N'Partner', N'Dependent', N'Other') THEN j.[role] ELSE N'Other' END,
    CASE WHEN TRY_CONVERT(uniqueidentifier, hd.[PrimaryMemberId]) = m.[Id] THEN 2 WHEN j.[role] = N'Primary' THEN 1 ELSE 0 END,
    d.[UpdatedAtUtc], h.[CreatedAtUtc]
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [householdId] nvarchar(max) '$.householdId', [memberId] nvarchar(max) '$.memberId', [role] nvarchar(max) '$.role') j
JOIN [ClubHouseholds] h ON h.[Id] = TRY_CONVERT(uniqueidentifier, j.[householdId]) AND h.[OrganizationId] = d.[OrganizationId]
JOIN [ClubMembers] m ON m.[Id] = TRY_CONVERT(uniqueidentifier, j.[memberId]) AND m.[OrganizationId] = d.[OrganizationId]
OUTER APPLY (
    SELECT JSON_VALUE(CASE WHEN ISJSON(hdoc.[Payload]) = 1 THEN hdoc.[Payload] END, '$.primaryMemberId') AS [PrimaryMemberId]
    FROM [ClubDocuments] hdoc WHERE hdoc.[Id] = h.[Id] AND hdoc.[Kind] = N'households'
) hd
WHERE d.[Kind] = N'household-members' AND d.[OrganizationId] IS NOT NULL;

INSERT INTO @links ([Id], [OrganizationId], [HouseholdId], [MemberId], [Role], [WantsPrimary], [At], [HouseholdAt])
SELECT NEWID(), h.[OrganizationId], h.[Id], m.[Id], N'Other', 2, h.[CreatedAtUtc], h.[CreatedAtUtc]
FROM [ClubHouseholds] h
JOIN [ClubDocuments] hdoc ON hdoc.[Id] = h.[Id] AND hdoc.[Kind] = N'households'
JOIN [ClubMembers] m ON m.[OrganizationId] = h.[OrganizationId]
    AND m.[Id] = TRY_CONVERT(uniqueidentifier, JSON_VALUE(CASE WHEN ISJSON(hdoc.[Payload]) = 1 THEN hdoc.[Payload] END, '$.primaryMemberId'))
WHERE NOT EXISTS (SELECT 1 FROM @links l WHERE l.[HouseholdId] = h.[Id] AND l.[MemberId] = m.[Id]);

-- Each member once (their first household), and one primary per household.
WITH kept AS (
    SELECT l.*, ROW_NUMBER() OVER (PARTITION BY l.[OrganizationId], l.[MemberId] ORDER BY l.[HouseholdAt], l.[At], l.[Id]) AS [MemberRank]
    FROM @links l
    WHERE NOT EXISTS (SELECT 1 FROM [ClubHouseholdMembers] x WHERE x.[OrganizationId] = l.[OrganizationId] AND x.[MemberId] = l.[MemberId])
        AND NOT EXISTS (SELECT 1 FROM [ClubHouseholdMembers] x WHERE x.[Id] = l.[Id])
),
ranked AS (
    SELECT k.*, CASE WHEN k.[WantsPrimary] > 0
        THEN ROW_NUMBER() OVER (PARTITION BY k.[HouseholdId], CASE WHEN k.[WantsPrimary] > 0 THEN 1 ELSE 0 END
                                ORDER BY k.[WantsPrimary] DESC, k.[At], k.[Id]) END AS [PrimaryRank]
    FROM kept k
    WHERE k.[MemberRank] = 1
)
INSERT INTO [ClubHouseholdMembers] ([Id], [OrganizationId], [HouseholdId], [MemberId], [ContactId], [Role], [IsPrimary],
    [RollUpBilling], [Source], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc])
SELECT r.[Id], r.[OrganizationId], r.[HouseholdId], r.[MemberId], NULL,
    CASE WHEN pr.[IsPrimary] = 1 THEN N'Primary' ELSE r.[Role] END,
    pr.[IsPrimary], 0, N'moved', r.[At], NULL, NULL
FROM ranked r
CROSS APPLY (SELECT CASE WHEN r.[PrimaryRank] = 1
    AND NOT EXISTS (SELECT 1 FROM [ClubHouseholdMembers] x WHERE x.[HouseholdId] = r.[HouseholdId] AND x.[IsPrimary] = 1)
    THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [IsPrimary]) pr;

-- ── couples become households ────────────────────────────────────────────────
DECLARE @org uniqueidentifier, @a uniqueidentifier, @b uniqueidentifier, @type nvarchar(20), @at datetime2;
DECLARE @ma uniqueidentifier, @mb uniqueidentifier, @ha uniqueidentifier, @hb uniqueidentifier, @h uniqueidentifier, @role nvarchar(20);
DECLARE couples CURSOR LOCAL FAST_FORWARD FOR
    SELECT r.[OrganizationId], r.[ContactId], r.[RelatedContactId], r.[Type], r.[CreatedAtUtc]
    FROM [ContactRelationships] r
    JOIN [Contacts] ca ON ca.[Id] = r.[ContactId] AND ca.[OrganizationId] = r.[OrganizationId] AND ca.[IsArchived] = 0
    JOIN [Contacts] cb ON cb.[Id] = r.[RelatedContactId] AND cb.[OrganizationId] = r.[OrganizationId] AND cb.[IsArchived] = 0
    WHERE r.[OrganizationId] IS NOT NULL AND r.[Type] IN (N'spouse', N'partner') AND r.[ContactId] <> r.[RelatedContactId]
    ORDER BY r.[CreatedAtUtc], r.[Id];
OPEN couples;
FETCH NEXT FROM couples INTO @org, @a, @b, @type, @at;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @role = CASE WHEN @type = N'spouse' THEN N'Spouse' ELSE N'Partner' END;
    -- A person who is a member is linked as the member (the first to join, when several share a contact).
    SET @ma = (SELECT TOP 1 m.[Id] FROM [ClubMembers] m WHERE m.[OrganizationId] = @org AND m.[ContactId] = @a ORDER BY m.[JoinedAtUtc], m.[Id]);
    SET @mb = (SELECT TOP 1 m.[Id] FROM [ClubMembers] m WHERE m.[OrganizationId] = @org AND m.[ContactId] = @b ORDER BY m.[JoinedAtUtc], m.[Id]);
    -- Each one's household, as a contact or through a member linked to them.
    SET @ha = (SELECT TOP 1 x.[HouseholdId] FROM [ClubHouseholdMembers] x LEFT JOIN [ClubMembers] xm ON xm.[Id] = x.[MemberId]
               WHERE x.[OrganizationId] = @org AND (x.[ContactId] = @a OR xm.[ContactId] = @a));
    SET @hb = (SELECT TOP 1 x.[HouseholdId] FROM [ClubHouseholdMembers] x LEFT JOIN [ClubMembers] xm ON xm.[Id] = x.[MemberId]
               WHERE x.[OrganizationId] = @org AND (x.[ContactId] = @b OR xm.[ContactId] = @b));

    IF @ha IS NULL AND @hb IS NULL
    BEGIN
        SET @h = NEWID();
        INSERT INTO [ClubHouseholds] ([Id], [OrganizationId], [Name], [Notes], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [Version])
        SELECT @h, @org,
            LEFT(COALESCE(NULLIF(LTRIM(RTRIM(CASE WHEN CHARINDEX(N' ', LTRIM(RTRIM(c.[Name]))) > 0
                THEN RIGHT(LTRIM(RTRIM(c.[Name])), CHARINDEX(N' ', REVERSE(LTRIM(RTRIM(c.[Name])))) - 1)
                ELSE c.[Name] END)), N''), N'Household'), 200),
            NULL, @at, NULL, NULL, NEWID()
        FROM [Contacts] c WHERE c.[Id] = @a;
        INSERT INTO [ClubHouseholdMembers] ([Id], [OrganizationId], [HouseholdId], [MemberId], [ContactId], [Role], [IsPrimary],
            [RollUpBilling], [Source], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc])
        VALUES (NEWID(), @org, @h, @ma, CASE WHEN @ma IS NULL THEN @a END, N'Primary', 1, 0, N'couple', @at, NULL, NULL),
               (NEWID(), @org, @h, @mb, CASE WHEN @mb IS NULL THEN @b END, @role, 0, 0, N'couple', @at, NULL, NULL);
    END
    ELSE IF @ha IS NOT NULL AND @hb IS NULL
        AND EXISTS (SELECT 1 FROM [ClubHouseholdMembers] x LEFT JOIN [ClubMembers] xm ON xm.[Id] = x.[MemberId]
                    WHERE x.[HouseholdId] = @ha AND x.[IsPrimary] = 1 AND (x.[ContactId] = @a OR xm.[ContactId] = @a))
        AND NOT EXISTS (SELECT 1 FROM [ClubHouseholdMembers] x WHERE x.[HouseholdId] = @ha AND x.[Role] IN (N'Spouse', N'Partner'))
    BEGIN
        INSERT INTO [ClubHouseholdMembers] ([Id], [OrganizationId], [HouseholdId], [MemberId], [ContactId], [Role], [IsPrimary],
            [RollUpBilling], [Source], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc])
        VALUES (NEWID(), @org, @ha, @mb, CASE WHEN @mb IS NULL THEN @b END, @role, 0, 0, N'couple', @at, NULL, NULL);
    END
    ELSE IF @hb IS NOT NULL AND @ha IS NULL
        AND EXISTS (SELECT 1 FROM [ClubHouseholdMembers] x LEFT JOIN [ClubMembers] xm ON xm.[Id] = x.[MemberId]
                    WHERE x.[HouseholdId] = @hb AND x.[IsPrimary] = 1 AND (x.[ContactId] = @b OR xm.[ContactId] = @b))
        AND NOT EXISTS (SELECT 1 FROM [ClubHouseholdMembers] x WHERE x.[HouseholdId] = @hb AND x.[Role] IN (N'Spouse', N'Partner'))
    BEGIN
        INSERT INTO [ClubHouseholdMembers] ([Id], [OrganizationId], [HouseholdId], [MemberId], [ContactId], [Role], [IsPrimary],
            [RollUpBilling], [Source], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc])
        VALUES (NEWID(), @org, @hb, @ma, CASE WHEN @ma IS NULL THEN @a END, @role, 0, 0, N'couple', @at, NULL, NULL);
    END

    FETCH NEXT FROM couples INTO @org, @a, @b, @type, @at;
END
CLOSE couples;
DEALLOCATE couples;
";
    }
}
