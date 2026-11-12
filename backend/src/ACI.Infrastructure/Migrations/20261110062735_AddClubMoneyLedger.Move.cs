namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// The move of the club's money documents (ClubDocuments of kind charges, payments,
    /// gift-cards, gift-card-redemptions, tabs, subscriptions and donations) into the typed
    /// ledger tables this migration makes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the migration rather than a start-up data fix, so the move and the tables are one
    /// transaction, run once per database by the migration history: a move that fails leaves
    /// no tables behind, rather than a ledger the app would read as empty. It copies: the
    /// documents stay, unread, until a later release removes them. Every row it writes has
    /// an id worked out from the document it came from, and is written only when no row has
    /// that id, so running it again adds nothing.
    /// </para>
    /// <para>
    /// Balances come over unchanged. A charge, payment, redemption and so on becomes its own
    /// row, a voided one a void row beside it. Where the documents do not add up to what the
    /// club saw, a row says so instead of the figure moving: a charge marked paid with no
    /// payment on record gets a credit "paid before the ledger" that pays it; a member's
    /// opening or hand-corrected balance (ClubMembers.ManualBalance) becomes an opening
    /// entry; and anything still different from the house balance shown before the move (an
    /// overpayment the old records dropped, a payment against a deleted charge) is one
    /// adjustment, so every member's balance after the move is the one they had before; the
    /// money the old records dropped pays that adjustment, so it is not listed as owed. A
    /// gift card's value comes over the same way: its issue, its redemptions, an adjustment
    /// for any difference from what the card said was left, and its void.
    /// </para>
    /// <para>
    /// Frozen with the migration on purpose: it reads the documents as they were written up
    /// to this release, and nothing later should change what it did.
    /// </para>
    /// </remarks>
    public partial class AddClubMoneyLedger
    {
        /// <summary>A UTC date read from the document's JSON, or NULL when the field is missing or not a date.</summary>
        private static string Date(string field) =>
            $"CAST(SWITCHOFFSET(TRY_CONVERT(datetimeoffset(7), JSON_VALUE(doc.[Json], '$.{field}'), 127), '+00:00') AS datetime2(7))";

        /// <summary>A money amount from the document's JSON, to the cent, or NULL.</summary>
        private static string Money(string field) => $"TRY_CAST(JSON_VALUE(doc.[Json], '$.{field}') AS decimal(18,2))";

        private static string Text(string field, int max) => $"LEFT(NULLIF(LTRIM(RTRIM(JSON_VALUE(doc.[Json], '$.{field}'))), N''), {max})";

        private static string Id(string field) => $"TRY_CAST(JSON_VALUE(doc.[Json], '$.{field}') AS uniqueidentifier)";

        /// <summary>An id for a row the move makes, the same on every run: the MD5 of a label and the document it came from.</summary>
        private static string Derived(string label, string idExpression) =>
            $"CONVERT(uniqueidentifier, HASHBYTES('MD5', N'{label}:' + CONVERT(nvarchar(36), {idExpression})))";

        /// <summary>Who wrote the document, from the audit trail's "created" line when it has one.</summary>
        private static string Author(string entityType) => $@"
            OUTER APPLY (SELECT TOP 1 a.[ActorUserId], LEFT(a.[ActorName], 200) AS [ActorName]
                         FROM [AuditEvents] a WHERE a.[EntityType] = N'{entityType}' AND a.[EntityId] = doc.[Id] AND a.[Action] = N'created'
                         ORDER BY a.[CreatedAtUtc]) author";

        private const string SameWorkspace = "(x.[OrganizationId] = m.[OrganizationId] OR (x.[OrganizationId] IS NULL AND m.[OrganizationId] IS NULL))";

        public static readonly string MoveDocumentsSql = $@"
SET NOCOUNT ON;
DECLARE @now datetime2(7) = SYSUTCDATETIME();
DECLARE @empty uniqueidentifier = '00000000-0000-0000-0000-000000000000';

IF OBJECT_ID('tempdb..#LedgerDocs') IS NOT NULL DROP TABLE #LedgerDocs;
IF OBJECT_ID('tempdb..#LedgerCharges') IS NOT NULL DROP TABLE #LedgerCharges;
IF OBJECT_ID('tempdb..#LedgerPayments') IS NOT NULL DROP TABLE #LedgerPayments;
IF OBJECT_ID('tempdb..#LedgerCards') IS NOT NULL DROP TABLE #LedgerCards;
IF OBJECT_ID('tempdb..#LedgerRedemptions') IS NOT NULL DROP TABLE #LedgerRedemptions;
IF OBJECT_ID('tempdb..#LedgerTabs') IS NOT NULL DROP TABLE #LedgerTabs;
IF OBJECT_ID('tempdb..#LedgerDifferences') IS NOT NULL DROP TABLE #LedgerDifferences;

-- Every money document, read once, with its workspace's currency. One whose payload is not
-- JSON stays where it is: there is nothing in it to read.
SELECT d.[Id], d.[OrganizationId], d.[Kind], d.[Payload] AS [Json], d.[UpdatedAtUtc],
       CASE WHEN LEN(o.[Currency]) = 3 THEN UPPER(o.[Currency]) ELSE N'CHF' END AS [Currency]
INTO #LedgerDocs
FROM [ClubDocuments] d
LEFT JOIN [Organizations] o ON o.[Id] = d.[OrganizationId]
WHERE d.[Kind] IN (N'charges', N'payments', N'gift-cards', N'gift-card-redemptions', N'tabs', N'subscriptions', N'donations')
  AND ISJSON(d.[Payload]) = 1;

-- ── charges ──────────────────────────────────────────────────────────────────
SELECT doc.[Id], doc.[OrganizationId], doc.[Currency],
       ISNULL({Id("memberId")}, @empty) AS [MemberId],
       ISNULL({Text("memberName", 200)}, N'') AS [MemberName],
       CASE WHEN JSON_VALUE(doc.[Json], '$.kind') IN (N'Dues', N'Dining', N'Bar', N'Event', N'Spa', N'Booking', N'Retail', N'Other')
            THEN JSON_VALUE(doc.[Json], '$.kind') ELSE N'Other' END AS [Kind],
       ISNULL({Text("description", 500)}, N'Charge') AS [Description],
       ISNULL({Money("amount")}, 0) AS [Amount],
       ISNULL({Money("paidAmount")}, 0) AS [PaidAmount],
       ISNULL(JSON_VALUE(doc.[Json], '$.status'), N'') AS [Status],
       COALESCE({Date("postedAtUtc")}, doc.[UpdatedAtUtc]) AS [PostedAtUtc],
       {Date("dueAtUtc")} AS [DueAtUtc],
       {Date("paidAtUtc")} AS [PaidAtUtc],
       {Text("reference", 200)} AS [Reference],
       {Date("voidedAtUtc")} AS [VoidedAtUtc],
       {Id("voidedByUserId")} AS [VoidedByUserId],
       {Text("voidedByName", 200)} AS [VoidedByName],
       {Text("voidReason", 500)} AS [VoidReason],
       author.[ActorUserId] AS [CreatedByUserId], author.[ActorName] AS [CreatedByName]
INTO #LedgerCharges
FROM #LedgerDocs doc {Author("charge")}
WHERE doc.[Kind] = N'charges';

-- ── payments ─────────────────────────────────────────────────────────────────
SELECT doc.[Id], doc.[OrganizationId], doc.[Currency],
       {Id("memberId")} AS [MemberId],
       ISNULL({Text("memberName", 200)}, N'') AS [MemberName],
       CASE WHEN JSON_VALUE(doc.[Json], '$.method') IN (N'CardOnFile', N'Cash', N'Check', N'Wire', N'AppliedCredit')
            THEN JSON_VALUE(doc.[Json], '$.method') ELSE NULL END AS [Method],
       ISNULL({Money("amount")}, 0) AS [Amount],
       COALESCE({Date("receivedAtUtc")}, doc.[UpdatedAtUtc]) AS [ReceivedAtUtc],
       {Text("reference", 200)} AS [Reference],
       COALESCE({Id("chargeId")}, TRY_CAST(JSON_VALUE(doc.[Json], '$.appliedToChargeIds[0]') AS uniqueidentifier)) AS [ChargeId],
       ISNULL(JSON_VALUE(doc.[Json], '$.status'), N'') AS [Status],
       {Date("voidedAtUtc")} AS [VoidedAtUtc],
       {Id("voidedByUserId")} AS [VoidedByUserId],
       {Text("voidedByName", 200)} AS [VoidedByName],
       {Text("voidReason", 500)} AS [VoidReason],
       author.[ActorUserId] AS [CreatedByUserId], author.[ActorName] AS [CreatedByName]
INTO #LedgerPayments
FROM #LedgerDocs doc {Author("payment")}
WHERE doc.[Kind] = N'payments';

-- A payment's member is its own, or else its charge's.
UPDATE p SET [MemberId] = c.[MemberId], [MemberName] = CASE WHEN p.[MemberName] = N'' THEN c.[MemberName] ELSE p.[MemberName] END
FROM #LedgerPayments p JOIN #LedgerCharges c ON c.[Id] = p.[ChargeId]
WHERE p.[MemberId] IS NULL;
UPDATE #LedgerPayments SET [MemberId] = @empty WHERE [MemberId] IS NULL;

-- ── tabs ─────────────────────────────────────────────────────────────────────
SELECT doc.[Id], doc.[OrganizationId], doc.[Currency], doc.[Json],
       ISNULL({Id("memberId")}, @empty) AS [MemberId],
       ISNULL({Text("memberName", 200)}, N'') AS [MemberName],
       ISNULL({Text("memberTier", 32)}, N'') AS [MemberTier],
       ISNULL({Text("venue", 32)}, N'Bar') AS [Venue],
       {Text("serverName", 200)} AS [ServerName],
       {Text("notes", 1000)} AS [Notes],
       CASE WHEN JSON_VALUE(doc.[Json], '$.status') IN (N'Open', N'Closed', N'Voided') THEN JSON_VALUE(doc.[Json], '$.status') ELSE N'Open' END AS [Status],
       COALESCE({Date("openedAtUtc")}, doc.[UpdatedAtUtc]) AS [OpenedAtUtc],
       {Money("total")} AS [Total],
       {Date("closedAtUtc")} AS [ClosedAtUtc],
       {Id("closedByUserId")} AS [ClosedByUserId],
       {Text("closedByName", 200)} AS [ClosedByName],
       {Id("chargeId")} AS [ChargeId],
       {Id("loyaltyEntryId")} AS [LoyaltyEntryId],
       TRY_CAST(JSON_VALUE(doc.[Json], '$.pointsAwarded') AS int) AS [PointsAwarded],
       {Date("voidedAtUtc")} AS [VoidedAtUtc],
       {Id("voidedByUserId")} AS [VoidedByUserId],
       {Text("voidedByName", 200)} AS [VoidedByName],
       {Text("voidReason", 500)} AS [VoidReason],
       author.[ActorUserId] AS [CreatedByUserId], author.[ActorName] AS [CreatedByName]
INTO #LedgerTabs
FROM #LedgerDocs doc {Author("tab")}
WHERE doc.[Kind] = N'tabs';

INSERT INTO [ClubTabs] ([Id], [OrganizationId], [MemberId], [MemberName], [MemberTier], [Venue], [ServerName], [Notes], [Status],
    [OpenedAtUtc], [Total], [Currency], [ClosedAtUtc], [ClosedByUserId], [ClosedByName], [ChargeId], [LoyaltyEntryId], [PointsAwarded],
    [VoidedAtUtc], [VoidedByUserId], [VoidedByName], [VoidReason], [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [Version])
SELECT t.[Id], t.[OrganizationId], t.[MemberId], t.[MemberName], t.[MemberTier], t.[Venue], t.[ServerName], t.[Notes], t.[Status],
    t.[OpenedAtUtc],
    COALESCE(t.[Total], (SELECT ISNULL(SUM(ROUND(ISNULL(TRY_CAST(i.[quantity] AS decimal(9,2)), 0) * ISNULL(TRY_CAST(i.[unitPrice] AS decimal(18,2)), 0), 2)), 0)
                         FROM OPENJSON(t.[Json], '$.items') WITH ([quantity] nvarchar(64) '$.quantity', [unitPrice] nvarchar(64) '$.unitPrice') i), 0),
    t.[Currency], t.[ClosedAtUtc], t.[ClosedByUserId], t.[ClosedByName], t.[ChargeId], t.[LoyaltyEntryId], t.[PointsAwarded],
    t.[VoidedAtUtc], t.[VoidedByUserId], t.[VoidedByName], t.[VoidReason], t.[OpenedAtUtc], t.[CreatedByUserId], t.[CreatedByName], NEWID()
FROM #LedgerTabs t
WHERE NOT EXISTS (SELECT 1 FROM [ClubTabs] x WHERE x.[Id] = t.[Id]);

-- A tab's items, from the list in its document. Each gets an id from its tab and its
-- place on the list: an item's own id was never a row's, and a copied organization's tabs
-- carry the same ones as the original's.
INSERT INTO [ClubTabItems] ([Id], [OrganizationId], [TabId], [Name], [Quantity], [UnitPrice], [AddedAtUtc], [AddedByUserId], [AddedByName])
SELECT item.[Id], t.[OrganizationId], t.[Id], item.[Name], item.[Quantity], item.[UnitPrice], item.[AddedAtUtc], item.[AddedByUserId], item.[AddedByName]
FROM #LedgerTabs t
CROSS APPLY (
    SELECT CONVERT(uniqueidentifier, HASHBYTES('MD5', N'item:' + CONVERT(nvarchar(36), t.[Id]) + N':' + CONVERT(nvarchar(20), i.[Position]))) AS [Id],
           ISNULL(LEFT(NULLIF(LTRIM(RTRIM(i.[name])), N''), 200), N'Item') AS [Name],
           ISNULL(TRY_CAST(i.[quantity] AS decimal(9,2)), 0) AS [Quantity],
           ISNULL(TRY_CAST(i.[unitPrice] AS decimal(18,2)), 0) AS [UnitPrice],
           COALESCE(CAST(SWITCHOFFSET(TRY_CONVERT(datetimeoffset(7), i.[addedAtUtc], 127), '+00:00') AS datetime2(7)), t.[OpenedAtUtc]) AS [AddedAtUtc],
           TRY_CAST(i.[addedByUserId] AS uniqueidentifier) AS [AddedByUserId],
           LEFT(i.[addedByName], 200) AS [AddedByName]
    FROM (
        SELECT e.*, ROW_NUMBER() OVER (ORDER BY e.[addedAtUtc], e.[name], e.[id], e.[quantity], e.[unitPrice]) AS [Position]
        FROM OPENJSON(t.[Json], '$.items') WITH (
            [id] nvarchar(64) '$.id', [name] nvarchar(4000) '$.name', [quantity] nvarchar(64) '$.quantity',
            [unitPrice] nvarchar(64) '$.unitPrice', [addedAtUtc] nvarchar(64) '$.addedAtUtc',
            [addedByUserId] nvarchar(64) '$.addedByUserId', [addedByName] nvarchar(4000) '$.addedByName') e
    ) i
) item
WHERE NOT EXISTS (SELECT 1 FROM [ClubTabItems] x WHERE x.[Id] = item.[Id]);

-- ── subscriptions ────────────────────────────────────────────────────────────
INSERT INTO [ClubSubscriptions] ([Id], [OrganizationId], [MemberId], [MemberName], [Name], [Amount], [Currency], [Cadence], [Status],
    [StartedAtUtc], [NextChargeAtUtc], [LastChargedAtUtc], [Notes], [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [UpdatedAtUtc], [Version])
SELECT doc.[Id], doc.[OrganizationId], ISNULL({Id("memberId")}, @empty), ISNULL({Text("memberName", 200)}, N''),
    ISNULL({Text("name", 200)}, N'Subscription'), ISNULL({Money("amount")}, 0), doc.[Currency],
    CASE WHEN JSON_VALUE(doc.[Json], '$.cadence') IN (N'Monthly', N'Quarterly', N'SemiAnnual', N'Annual') THEN JSON_VALUE(doc.[Json], '$.cadence') ELSE N'Monthly' END,
    CASE WHEN JSON_VALUE(doc.[Json], '$.status') IN (N'Active', N'Paused', N'Cancelled', N'PastDue') THEN JSON_VALUE(doc.[Json], '$.status') ELSE N'Active' END,
    COALESCE({Date("startedAtUtc")}, doc.[UpdatedAtUtc]), {Date("nextChargeAtUtc")}, {Date("lastChargedAtUtc")}, {Text("notes", 1000)},
    COALESCE({Date("startedAtUtc")}, doc.[UpdatedAtUtc]), author.[ActorUserId], author.[ActorName], doc.[UpdatedAtUtc], NEWID()
FROM #LedgerDocs doc {Author("subscription")}
WHERE doc.[Kind] = N'subscriptions'
  AND NOT EXISTS (SELECT 1 FROM [ClubSubscriptions] x WHERE x.[Id] = doc.[Id]);

-- ── charges and their voids ──────────────────────────────────────────────────
INSERT INTO [ClubCharges] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Kind], [Description], [Amount], [Currency],
    [PostedAtUtc], [DueAtUtc], [Reference], [TabId], [CreatedAtUtc], [CreatedByUserId], [CreatedByName])
SELECT c.[Id], c.[OrganizationId], c.[MemberId], c.[MemberName], N'Charge', c.[Kind], c.[Description], c.[Amount], c.[Currency],
    c.[PostedAtUtc], c.[DueAtUtc], c.[Reference], tab.[Id], c.[PostedAtUtc], c.[CreatedByUserId], c.[CreatedByName]
FROM #LedgerCharges c
OUTER APPLY (SELECT TOP 1 t.[Id] FROM [ClubTabs] t WHERE t.[ChargeId] = c.[Id]
             AND NOT EXISTS (SELECT 1 FROM [ClubCharges] y WHERE y.[TabId] = t.[Id]) ORDER BY t.[Id]) tab
WHERE NOT EXISTS (SELECT 1 FROM [ClubCharges] x WHERE x.[Id] = c.[Id]);

INSERT INTO [ClubCharges] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Kind], [Description], [Amount], [Currency],
    [PostedAtUtc], [ReversesChargeId], [Reason], [CreatedAtUtc], [CreatedByUserId], [CreatedByName])
SELECT {Derived("void", "c.[Id]")}, c.[OrganizationId], c.[MemberId], c.[MemberName], N'Void', c.[Kind], c.[Description], -c.[Amount], c.[Currency],
    COALESCE(c.[VoidedAtUtc], @now), c.[Id], ISNULL(c.[VoidReason], N'Voided before the ledger'),
    COALESCE(c.[VoidedAtUtc], @now), c.[VoidedByUserId], c.[VoidedByName]
FROM #LedgerCharges c
WHERE c.[Status] = N'Voided'
  AND NOT EXISTS (SELECT 1 FROM [ClubCharges] x WHERE x.[ReversesChargeId] = c.[Id]);

-- ── payments, what they paid, and their voids ───────────────────────────────
INSERT INTO [ClubPayments] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Method], [Amount], [Currency],
    [ReceivedAtUtc], [Reference], [CreatedAtUtc], [CreatedByUserId], [CreatedByName])
SELECT p.[Id], p.[OrganizationId], p.[MemberId], p.[MemberName], N'Payment', p.[Method], p.[Amount], p.[Currency],
    p.[ReceivedAtUtc], p.[Reference], p.[ReceivedAtUtc], p.[CreatedByUserId], p.[CreatedByName]
FROM #LedgerPayments p
WHERE NOT EXISTS (SELECT 1 FROM [ClubPayments] x WHERE x.[Id] = p.[Id]);

-- What a standing payment paid its charge: no more than the charge's own record says it
-- was paid, oldest payment first. The rest of a payment the old records dropped (an
-- overpayment) stays on the payment, unspent, and the member's adjustment below keeps
-- their balance where it was.
WITH standing AS (
    SELECT p.[Id], p.[OrganizationId], p.[ChargeId], p.[Amount], p.[ReceivedAtUtc], c.[PaidAmount],
           SUM(p.[Amount]) OVER (PARTITION BY p.[ChargeId] ORDER BY p.[ReceivedAtUtc], p.[Id] ROWS UNBOUNDED PRECEDING) - p.[Amount] AS [Before]
    FROM #LedgerPayments p
    JOIN #LedgerCharges c ON c.[Id] = p.[ChargeId]
    WHERE p.[Status] <> N'Voided' AND c.[Status] <> N'Voided' AND p.[Amount] > 0
)
INSERT INTO [ClubPaymentAllocations] ([Id], [OrganizationId], [PaymentId], [ChargeId], [Amount], [CreatedAtUtc])
SELECT {Derived("alloc", "s.[Id]")}, s.[OrganizationId], s.[Id], s.[ChargeId],
       CASE WHEN s.[Amount] < s.[PaidAmount] - s.[Before] THEN s.[Amount] ELSE s.[PaidAmount] - s.[Before] END, s.[ReceivedAtUtc]
FROM standing s
WHERE s.[PaidAmount] - s.[Before] > 0
  AND NOT EXISTS (SELECT 1 FROM [ClubPaymentAllocations] x WHERE x.[Id] = {Derived("alloc", "s.[Id]")});

-- A voided payment: it paid its charge, and its void took that back.
INSERT INTO [ClubPayments] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Method], [Amount], [Currency],
    [ReceivedAtUtc], [ReversesPaymentId], [Reason], [CreatedAtUtc], [CreatedByUserId], [CreatedByName])
SELECT {Derived("void", "p.[Id]")}, p.[OrganizationId], p.[MemberId], p.[MemberName], N'Void', p.[Method], -p.[Amount], p.[Currency],
    COALESCE(p.[VoidedAtUtc], @now), p.[Id], ISNULL(p.[VoidReason], N'Voided before the ledger'),
    COALESCE(p.[VoidedAtUtc], @now), p.[VoidedByUserId], p.[VoidedByName]
FROM #LedgerPayments p
WHERE p.[Status] = N'Voided'
  AND NOT EXISTS (SELECT 1 FROM [ClubPayments] x WHERE x.[ReversesPaymentId] = p.[Id] AND x.[EntryType] = N'Void');

INSERT INTO [ClubPaymentAllocations] ([Id], [OrganizationId], [PaymentId], [ChargeId], [Amount], [CreatedAtUtc])
SELECT {Derived("alloc", "p.[Id]")}, p.[OrganizationId], p.[Id], p.[ChargeId], p.[Amount], p.[ReceivedAtUtc]
FROM #LedgerPayments p
JOIN #LedgerCharges c ON c.[Id] = p.[ChargeId]
WHERE p.[Status] = N'Voided' AND c.[Status] <> N'Voided' AND p.[Amount] > 0
  AND NOT EXISTS (SELECT 1 FROM [ClubPaymentAllocations] x WHERE x.[Id] = {Derived("alloc", "p.[Id]")});

INSERT INTO [ClubPaymentAllocations] ([Id], [OrganizationId], [PaymentId], [ChargeId], [Amount], [ReversalId], [CreatedAtUtc])
SELECT {Derived("release", "p.[Id]")}, p.[OrganizationId], p.[Id], p.[ChargeId], -p.[Amount], {Derived("void", "p.[Id]")}, COALESCE(p.[VoidedAtUtc], @now)
FROM #LedgerPayments p
JOIN #LedgerCharges c ON c.[Id] = p.[ChargeId]
WHERE p.[Status] = N'Voided' AND c.[Status] <> N'Voided' AND p.[Amount] > 0
  AND NOT EXISTS (SELECT 1 FROM [ClubPaymentAllocations] x WHERE x.[Id] = {Derived("release", "p.[Id]")});

-- A charge its record says was paid by more than the payments on record: a credit from
-- before the ledger pays the rest, so it reads as paid, as it did.
WITH owed AS (
    SELECT c.[Id], c.[OrganizationId], c.[MemberId], c.[MemberName], c.[Currency], c.[PostedAtUtc], c.[PaidAtUtc],
           c.[PaidAmount] - ISNULL((SELECT SUM(a.[Amount]) FROM [ClubPaymentAllocations] a WHERE a.[ChargeId] = c.[Id]), 0) AS [Unexplained]
    FROM #LedgerCharges c
    WHERE c.[Status] <> N'Voided' AND c.[PaidAmount] > 0
)
INSERT INTO [ClubPayments] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Amount], [Currency], [ReceivedAtUtc],
    [Description], [Reason], [CreatedAtUtc])
SELECT {Derived("paid", "o.[Id]")}, o.[OrganizationId], o.[MemberId], o.[MemberName], N'Opening', o.[Unexplained], o.[Currency],
    COALESCE(o.[PaidAtUtc], o.[PostedAtUtc]), N'Paid before the ledger', N'The charge was recorded as paid, with no payment kept for this part.',
    COALESCE(o.[PaidAtUtc], o.[PostedAtUtc])
FROM owed o
WHERE o.[Unexplained] > 0
  AND NOT EXISTS (SELECT 1 FROM [ClubPayments] x WHERE x.[Id] = {Derived("paid", "o.[Id]")});

INSERT INTO [ClubPaymentAllocations] ([Id], [OrganizationId], [PaymentId], [ChargeId], [Amount], [CreatedAtUtc])
SELECT {Derived("paidalloc", "p.[Id]")}, p.[OrganizationId], p.[Id], c.[Id], p.[Amount], p.[ReceivedAtUtc]
FROM #LedgerCharges c
JOIN [ClubPayments] p ON p.[Id] = {Derived("paid", "c.[Id]")}
WHERE NOT EXISTS (SELECT 1 FROM [ClubPaymentAllocations] x WHERE x.[Id] = {Derived("paidalloc", "p.[Id]")});

-- ── gift cards ───────────────────────────────────────────────────────────────
SELECT doc.[Id], doc.[OrganizationId], doc.[Currency],
       UPPER(ISNULL({Text("code", 32)}, N'GIFT-' + UPPER(LEFT(CONVERT(nvarchar(36), doc.[Id]), 8)))) AS [Code],
       ISNULL({Money("faceValue")}, 0) AS [FaceValue],
       ISNULL({Money("remainingValue")}, 0) AS [RemainingValue],
       {Money("writtenOffValue")} AS [WrittenOffValue],
       ISNULL(JSON_VALUE(doc.[Json], '$.status'), N'') AS [Status],
       {Id("purchaserMemberId")} AS [PurchaserMemberId],
       ISNULL({Text("purchaserName", 200)}, N'') AS [PurchaserName],
       ISNULL({Text("recipientName", 200)}, N'') AS [RecipientName],
       {Text("recipientEmail", 256)} AS [RecipientEmail],
       {Text("message", 1000)} AS [Message],
       COALESCE({Date("issuedAtUtc")}, doc.[UpdatedAtUtc]) AS [IssuedAtUtc],
       {Date("expiresAtUtc")} AS [ExpiresAtUtc],
       {Date("voidedAtUtc")} AS [VoidedAtUtc],
       {Id("voidedByUserId")} AS [VoidedByUserId],
       {Text("voidedByName", 200)} AS [VoidedByName],
       {Text("voidReason", 500)} AS [VoidReason],
       author.[ActorUserId] AS [CreatedByUserId], author.[ActorName] AS [CreatedByName]
INTO #LedgerCards
FROM #LedgerDocs doc {Author("gift_card")}
WHERE doc.[Kind] = N'gift-cards';

-- A code is one card's in its workspace. A second card that came in with a code already
-- taken (the client used to choose them) keeps it with a piece of its own id added.
WITH numbered AS (
    SELECT c.[Id], ROW_NUMBER() OVER (PARTITION BY c.[OrganizationId], c.[Code] ORDER BY c.[IssuedAtUtc], c.[Id]) AS [N] FROM #LedgerCards c
)
UPDATE c SET [Code] = LEFT(c.[Code], 23) + N'-' + UPPER(LEFT(CONVERT(nvarchar(36), c.[Id]), 8))
FROM #LedgerCards c JOIN numbered n ON n.[Id] = c.[Id]
WHERE n.[N] > 1;

INSERT INTO [ClubGiftCards] ([Id], [OrganizationId], [Code], [FaceValue], [Currency], [PurchaserMemberId], [PurchaserName], [RecipientName],
    [RecipientEmail], [Message], [IssuedAtUtc], [ExpiresAtUtc], [Status], [LastTransactionAtUtc], [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [Version])
SELECT c.[Id], c.[OrganizationId], c.[Code], c.[FaceValue], c.[Currency], c.[PurchaserMemberId], c.[PurchaserName], c.[RecipientName],
    c.[RecipientEmail], c.[Message], c.[IssuedAtUtc], c.[ExpiresAtUtc], CASE WHEN c.[Status] = N'Voided' THEN N'Voided' ELSE N'Active' END,
    @now, c.[IssuedAtUtc], c.[CreatedByUserId], c.[CreatedByName], NEWID()
FROM #LedgerCards c
WHERE NOT EXISTS (SELECT 1 FROM [ClubGiftCards] x WHERE x.[Id] = c.[Id])
  AND NOT EXISTS (SELECT 1 FROM [ClubGiftCards] x WHERE x.[Code] = c.[Code]
                  AND (x.[OrganizationId] = c.[OrganizationId] OR (x.[OrganizationId] IS NULL AND c.[OrganizationId] IS NULL)));

INSERT INTO [ClubGiftCardTransactions] ([Id], [OrganizationId], [GiftCardId], [Type], [Amount], [Currency], [AtUtc], [CreatedAtUtc], [CreatedByUserId], [CreatedByName])
SELECT {Derived("issue", "c.[Id]")}, c.[OrganizationId], c.[Id], N'Issue', c.[FaceValue], c.[Currency], c.[IssuedAtUtc], c.[IssuedAtUtc], c.[CreatedByUserId], c.[CreatedByName]
FROM #LedgerCards c
WHERE EXISTS (SELECT 1 FROM [ClubGiftCards] g WHERE g.[Id] = c.[Id])
  AND NOT EXISTS (SELECT 1 FROM [ClubGiftCardTransactions] x WHERE x.[Id] = {Derived("issue", "c.[Id]")});

SELECT doc.[Id], doc.[OrganizationId], doc.[Currency],
       {Id("giftCardId")} AS [GiftCardId],
       ISNULL({Money("amount")}, 0) AS [Amount],
       COALESCE({Date("redeemedAtUtc")}, doc.[UpdatedAtUtc]) AS [RedeemedAtUtc],
       {Text("redeemedByName", 200)} AS [RedeemedByName],
       {Text("appliedTo", 500)} AS [AppliedTo],
       {Text("idempotencyKey", 100)} AS [IdempotencyKey],
       author.[ActorUserId] AS [CreatedByUserId], author.[ActorName] AS [CreatedByName]
INTO #LedgerRedemptions
FROM #LedgerDocs doc {Author("gift_card_redemption")}
WHERE doc.[Kind] = N'gift-card-redemptions';

-- A retry key belongs to one redemption in a workspace; a repeat (which no app wrote) keeps none.
WITH keyed AS (
    SELECT r.[Id], ROW_NUMBER() OVER (PARTITION BY r.[OrganizationId], r.[IdempotencyKey] ORDER BY r.[RedeemedAtUtc], r.[Id]) AS [N]
    FROM #LedgerRedemptions r WHERE r.[IdempotencyKey] IS NOT NULL
)
UPDATE r SET [IdempotencyKey] = NULL FROM #LedgerRedemptions r JOIN keyed k ON k.[Id] = r.[Id] WHERE k.[N] > 1;

INSERT INTO [ClubGiftCardTransactions] ([Id], [OrganizationId], [GiftCardId], [Type], [Amount], [Currency], [AtUtc], [AppliedTo], [RedeemedByName],
    [IdempotencyKey], [CreatedAtUtc], [CreatedByUserId], [CreatedByName])
SELECT r.[Id], g.[OrganizationId], g.[Id], N'Redeem', -r.[Amount], g.[Currency], r.[RedeemedAtUtc], r.[AppliedTo], r.[RedeemedByName],
    CASE WHEN EXISTS (SELECT 1 FROM [ClubGiftCardTransactions] k WHERE k.[IdempotencyKey] = r.[IdempotencyKey]
                      AND (k.[OrganizationId] = g.[OrganizationId] OR (k.[OrganizationId] IS NULL AND g.[OrganizationId] IS NULL))) THEN NULL
         ELSE r.[IdempotencyKey] END,
    r.[RedeemedAtUtc], r.[CreatedByUserId], r.[CreatedByName]
FROM #LedgerRedemptions r
JOIN [ClubGiftCards] g ON g.[Id] = r.[GiftCardId]
WHERE NOT EXISTS (SELECT 1 FROM [ClubGiftCardTransactions] x WHERE x.[Id] = r.[Id]);

-- What a card's records said was left, where its issue and redemptions add up to
-- something else. A voided card: what its void wrote off.
WITH target AS (
    SELECT c.[Id], c.[OrganizationId], c.[Currency],
           CASE WHEN c.[Status] = N'Voided' THEN COALESCE(c.[WrittenOffValue], c.[RemainingValue]) ELSE c.[RemainingValue] END AS [Left],
           (SELECT ISNULL(SUM(t.[Amount]), 0) FROM [ClubGiftCardTransactions] t WHERE t.[GiftCardId] = c.[Id]) AS [Moved]
    FROM #LedgerCards c
    WHERE EXISTS (SELECT 1 FROM [ClubGiftCards] g WHERE g.[Id] = c.[Id])
      AND NOT EXISTS (SELECT 1 FROM [ClubGiftCardTransactions] v WHERE v.[GiftCardId] = c.[Id] AND v.[Type] IN (N'Void', N'Adjustment'))
)
INSERT INTO [ClubGiftCardTransactions] ([Id], [OrganizationId], [GiftCardId], [Type], [Amount], [Currency], [AtUtc], [Reason], [CreatedAtUtc])
SELECT {Derived("adjust", "t.[Id]")}, t.[OrganizationId], t.[Id], N'Adjustment', t.[Left] - t.[Moved], t.[Currency], @now,
    N'Brought over: what the old records said was left on the card.', @now
FROM target t
WHERE t.[Left] <> t.[Moved]
  AND NOT EXISTS (SELECT 1 FROM [ClubGiftCardTransactions] x WHERE x.[Id] = {Derived("adjust", "t.[Id]")});

INSERT INTO [ClubGiftCardTransactions] ([Id], [OrganizationId], [GiftCardId], [Type], [Amount], [Currency], [AtUtc], [Reason], [CreatedAtUtc], [CreatedByUserId], [CreatedByName])
SELECT {Derived("void", "c.[Id]")}, c.[OrganizationId], c.[Id], N'Void',
    -(SELECT ISNULL(SUM(t.[Amount]), 0) FROM [ClubGiftCardTransactions] t WHERE t.[GiftCardId] = c.[Id]),
    c.[Currency], COALESCE(c.[VoidedAtUtc], @now), ISNULL(c.[VoidReason], N'Voided before the ledger'), COALESCE(c.[VoidedAtUtc], @now),
    c.[VoidedByUserId], c.[VoidedByName]
FROM #LedgerCards c
WHERE c.[Status] = N'Voided'
  AND EXISTS (SELECT 1 FROM [ClubGiftCards] g WHERE g.[Id] = c.[Id])
  AND NOT EXISTS (SELECT 1 FROM [ClubGiftCardTransactions] x WHERE x.[GiftCardId] = c.[Id] AND x.[Type] = N'Void');

-- ── donations ────────────────────────────────────────────────────────────────
INSERT INTO [ClubDonations] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Cause], [Amount], [Currency], [ReceivedAtUtc],
    [IsAnonymous], [Message], [Acknowledged], [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [Version])
SELECT doc.[Id], doc.[OrganizationId], {Id("memberId")}, {Text("memberName", 200)}, N'Donation', ISNULL({Text("cause", 60)}, N'Other'),
    ISNULL({Money("amount")}, 0), doc.[Currency], COALESCE({Date("receivedAtUtc")}, doc.[UpdatedAtUtc]),
    CASE WHEN JSON_VALUE(doc.[Json], '$.isAnonymous') = N'true' THEN 1 ELSE 0 END, {Text("message", 1000)},
    CASE WHEN JSON_VALUE(doc.[Json], '$.acknowledged') = N'true' THEN 1 ELSE 0 END,
    COALESCE({Date("receivedAtUtc")}, doc.[UpdatedAtUtc]), author.[ActorUserId], author.[ActorName], NEWID()
FROM #LedgerDocs doc {Author("donation")}
WHERE doc.[Kind] = N'donations'
  AND NOT EXISTS (SELECT 1 FROM [ClubDonations] x WHERE x.[Id] = doc.[Id]);

-- ── members' house accounts ──────────────────────────────────────────────────
INSERT INTO [ClubHouseAccounts] ([Id], [OrganizationId], [MemberId], [Currency], [OpenedAtUtc], [Version])
SELECT {Derived("account", "m.[Id]")}, m.[OrganizationId], m.[Id],
    CASE WHEN LEN(o.[Currency]) = 3 THEN UPPER(o.[Currency]) ELSE N'CHF' END,
    CASE WHEN m.[JoinedAtUtc] < @now THEN m.[JoinedAtUtc] ELSE @now END, NEWID()
FROM [ClubMembers] m
LEFT JOIN [Organizations] o ON o.[Id] = m.[OrganizationId]
WHERE NOT EXISTS (SELECT 1 FROM [ClubHouseAccounts] x WHERE x.[MemberId] = m.[Id]);

-- The balance a member was brought over with, and corrections made by hand, as an
-- opening entry: owed when negative, a credit when positive.
INSERT INTO [ClubCharges] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Kind], [Description], [Amount], [Currency],
    [PostedAtUtc], [Reason], [CreatedAtUtc])
SELECT {Derived("opening", "m.[Id]")}, m.[OrganizationId], m.[Id], LEFT(LTRIM(RTRIM(m.[FirstName] + N' ' + m.[LastName])), 200), N'Opening', N'Other',
    N'Opening balance brought over', -m.[ManualBalance], a.[Currency],
    CASE WHEN m.[JoinedAtUtc] < @now THEN m.[JoinedAtUtc] ELSE @now END,
    N'The balance the member was brought over with, and corrections made by hand, before the ledger.', @now
FROM [ClubMembers] m
JOIN [ClubHouseAccounts] a ON a.[MemberId] = m.[Id]
WHERE m.[ManualBalance] < 0
  AND NOT EXISTS (SELECT 1 FROM [ClubCharges] x WHERE x.[Id] = {Derived("opening", "m.[Id]")})
  AND NOT EXISTS (SELECT 1 FROM [ClubPayments] x WHERE x.[Id] = {Derived("opening", "m.[Id]")});

INSERT INTO [ClubPayments] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Amount], [Currency], [ReceivedAtUtc],
    [Description], [Reason], [CreatedAtUtc])
SELECT {Derived("opening", "m.[Id]")}, m.[OrganizationId], m.[Id], LEFT(LTRIM(RTRIM(m.[FirstName] + N' ' + m.[LastName])), 200), N'Opening',
    m.[ManualBalance], a.[Currency], CASE WHEN m.[JoinedAtUtc] < @now THEN m.[JoinedAtUtc] ELSE @now END,
    N'Opening balance brought over', N'The balance the member was brought over with, and corrections made by hand, before the ledger.', @now
FROM [ClubMembers] m
JOIN [ClubHouseAccounts] a ON a.[MemberId] = m.[Id]
WHERE m.[ManualBalance] > 0
  AND NOT EXISTS (SELECT 1 FROM [ClubCharges] x WHERE x.[Id] = {Derived("opening", "m.[Id]")})
  AND NOT EXISTS (SELECT 1 FROM [ClubPayments] x WHERE x.[Id] = {Derived("opening", "m.[Id]")});

-- Whatever is left between the entries and the house balance the club saw before the
-- move becomes one adjustment, so the balance is the same after it.
WITH ledger AS (
    SELECT m.[Id], m.[OrganizationId], m.[FirstName], m.[LastName], m.[HouseAccountBalance], a.[Currency],
           ISNULL((SELECT SUM(x.[Amount]) FROM [ClubPayments] x WHERE x.[MemberId] = m.[Id] AND {SameWorkspace}), 0)
         - ISNULL((SELECT SUM(x.[Amount]) FROM [ClubCharges] x WHERE x.[MemberId] = m.[Id] AND {SameWorkspace}), 0) AS [Balance]
    FROM [ClubMembers] m
    JOIN [ClubHouseAccounts] a ON a.[MemberId] = m.[Id]
)
SELECT l.[Id], l.[OrganizationId], LEFT(LTRIM(RTRIM(l.[FirstName] + N' ' + l.[LastName])), 200) AS [MemberName], l.[Currency],
       l.[HouseAccountBalance] - l.[Balance] AS [Difference]
INTO #LedgerDifferences
FROM ledger l
WHERE l.[HouseAccountBalance] <> l.[Balance];

INSERT INTO [ClubPayments] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Amount], [Currency], [ReceivedAtUtc],
    [Description], [Reason], [CreatedAtUtc])
SELECT {Derived("reconcile", "d.[Id]")}, d.[OrganizationId], d.[Id], d.[MemberName], N'Adjustment', d.[Difference], d.[Currency], @now,
    N'Brought over to match the house balance before the ledger',
    N'The old records did not add up to the balance shown: an overpayment they dropped, a payment against a deleted charge, or a balance set by hand.', @now
FROM #LedgerDifferences d
WHERE d.[Difference] > 0
  AND NOT EXISTS (SELECT 1 FROM [ClubPayments] x WHERE x.[Id] = {Derived("reconcile", "d.[Id]")})
  AND NOT EXISTS (SELECT 1 FROM [ClubCharges] x WHERE x.[Id] = {Derived("reconcile", "d.[Id]")});

INSERT INTO [ClubCharges] ([Id], [OrganizationId], [MemberId], [MemberName], [EntryType], [Kind], [Description], [Amount], [Currency],
    [PostedAtUtc], [Reason], [CreatedAtUtc])
SELECT {Derived("reconcile", "d.[Id]")}, d.[OrganizationId], d.[Id], d.[MemberName], N'Adjustment', N'Other',
    N'Brought over to match the house balance before the ledger', -d.[Difference], d.[Currency], @now,
    N'The old records did not add up to the balance shown: an overpayment they dropped, a payment against a deleted charge, or a balance set by hand.', @now
FROM #LedgerDifferences d
WHERE d.[Difference] < 0
  AND NOT EXISTS (SELECT 1 FROM [ClubPayments] x WHERE x.[Id] = {Derived("reconcile", "d.[Id]")})
  AND NOT EXISTS (SELECT 1 FROM [ClubCharges] x WHERE x.[Id] = {Derived("reconcile", "d.[Id]")});

-- Entries that add up to more credit than the balance the club saw hold money the old
-- records dropped: the part of an overpayment they did not keep, a payment against a
-- charge since deleted. That money pays the adjustment just made, oldest payment first.
-- Left unpaid, the adjustment listed the member as owing what they never owed (and the
-- club's outstanding with it), beside credit their balance never had: the two only
-- cancelled out in the balance.
WITH reconcile AS (
    SELECT d.[Id] AS [MemberId], d.[OrganizationId], {Derived("reconcile", "d.[Id]")} AS [ChargeId], -d.[Difference] AS [Amount]
    FROM #LedgerDifferences d
    WHERE d.[Difference] < 0
), unspent AS (
    SELECT p.[Id], p.[OrganizationId], p.[ReceivedAtUtc], r.[ChargeId],
           r.[Amount] - ISNULL((SELECT SUM(a.[Amount]) FROM [ClubPaymentAllocations] a WHERE a.[ChargeId] = r.[ChargeId]), 0) AS [Owed],
           p.[Amount] + ISNULL((SELECT SUM(v.[Amount]) FROM [ClubPayments] v WHERE v.[ReversesPaymentId] = p.[Id]), 0)
         - ISNULL((SELECT SUM(a.[Amount]) FROM [ClubPaymentAllocations] a WHERE a.[PaymentId] = p.[Id]), 0) AS [Left]
    FROM reconcile r
    JOIN [ClubPayments] p ON p.[MemberId] = r.[MemberId]
        AND (p.[OrganizationId] = r.[OrganizationId] OR (p.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL))
    WHERE p.[EntryType] = N'Payment' AND p.[ReversesPaymentId] IS NULL
      AND EXISTS (SELECT 1 FROM [ClubCharges] c WHERE c.[Id] = r.[ChargeId])
), spendable AS (
    SELECT u.*, SUM(u.[Left]) OVER (PARTITION BY u.[ChargeId] ORDER BY u.[ReceivedAtUtc], u.[Id] ROWS UNBOUNDED PRECEDING) - u.[Left] AS [Before]
    FROM unspent u
    WHERE u.[Left] > 0
)
INSERT INTO [ClubPaymentAllocations] ([Id], [OrganizationId], [PaymentId], [ChargeId], [Amount], [CreatedAtUtc])
SELECT {Derived("reconciled", "s.[Id]")}, s.[OrganizationId], s.[Id], s.[ChargeId],
       CASE WHEN s.[Left] < s.[Owed] - s.[Before] THEN s.[Left] ELSE s.[Owed] - s.[Before] END, @now
FROM spendable s
WHERE s.[Owed] - s.[Before] > 0
  AND NOT EXISTS (SELECT 1 FROM [ClubPaymentAllocations] x WHERE x.[Id] = {Derived("reconciled", "s.[Id]")});

DROP TABLE #LedgerDifferences;
DROP TABLE #LedgerRedemptions;
DROP TABLE #LedgerCards;
DROP TABLE #LedgerTabs;
DROP TABLE #LedgerPayments;
DROP TABLE #LedgerCharges;
DROP TABLE #LedgerDocs;
";
    }
}
