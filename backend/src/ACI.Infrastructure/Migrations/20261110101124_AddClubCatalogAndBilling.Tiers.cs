namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// The membership tiers each workspace already had, carried into the price catalogue this
    /// migration makes, so nothing changes for anyone until someone edits the catalogue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A workspace's tiers were the ones its hospitality settings document listed (the
    /// "tiers" array the settings page saved: name, monthly dues, initiation fee, loyalty
    /// multiplier, benefits), in the order it listed them; one that never saved its settings
    /// had the four tiers the page has always shown (Bronze, Silver, Gold, Platinum, at 49,
    /// 129, 299 and 599 a month). Each becomes a catalogue item of kind Membership with the
    /// same name, dues, fee, multiplier and benefits, billed monthly, in the workspace's
    /// currency, with no VAT recorded (none was before). Members keep their tier, which is
    /// still the tier's name, and their own dues: only the defaults for new members come
    /// from the catalogue.
    /// </para>
    /// <para>
    /// In the migration, with the table it fills, so a database never has the catalogue
    /// without its tiers. It copies: the settings documents stay as they are. It writes tiers
    /// only for a workspace that has none in the catalogue yet, so running it again adds
    /// nothing, even after a club has renamed its tiers; on a new database there is nothing
    /// to carry. A workspace made later gets the same default tiers from the app the first
    /// time it reads its catalogue (ClubCatalogService), by the same rule.
    /// </para>
    /// <para>
    /// Frozen with the migration on purpose: the defaults are written out here rather than
    /// read from code that may change later.
    /// </para>
    /// </remarks>
    public partial class AddClubCatalogAndBilling
    {
        // The workspace's currency when it reads as a code, else francs.
        private const string Currency = "CASE WHEN o.[Currency] LIKE N'[A-Z][A-Z][A-Z]' THEN o.[Currency] ELSE N'CHF' END";

        // Workspaces with no tier in the catalogue yet: the ones to carry over.
        private const string NoTiersYet =
            "NOT EXISTS (SELECT 1 FROM [ClubCatalogItems] c WHERE c.[OrganizationId] = o.[Id] AND c.[Kind] = N'Membership')";

        /// <summary>The carry-over, on its own, so a test can run it a second time and see it add nothing.</summary>
        public static readonly string CarryOverTiersSql = $@"
DECLARE @now datetime2 = SYSUTCDATETIME();

-- The tiers a workspace's saved settings list, in their order. A tier named twice is
-- taken once, the first time; a name longer than a member's tier can hold is cut to fit.
WITH settings AS (
    SELECT d.[OrganizationId], d.[Payload],
        ROW_NUMBER() OVER (PARTITION BY d.[OrganizationId] ORDER BY d.[UpdatedAtUtc] DESC, d.[Id]) AS [Latest]
    FROM [ClubDocuments] d
    WHERE d.[Kind] = N'hospitality-settings' AND d.[OrganizationId] IS NOT NULL
        AND ISJSON(d.[Payload]) = 1 AND LEFT(LTRIM(d.[Payload]), 1) = N'{{'
),
listed AS (
    SELECT o.[Id] AS [OrganizationId], {Currency} AS [Currency],
        LEFT(LTRIM(RTRIM(t.[tier])), 32) AS [Name],
        TRY_CONVERT(decimal(18,4), t.[monthlyDues]) AS [Dues],
        TRY_CONVERT(decimal(18,4), t.[initiationFee]) AS [Fee],
        TRY_CONVERT(decimal(18,4), t.[loyaltyMultiplier]) AS [Multiplier],
        CASE WHEN ISJSON(t.[benefits]) = 1 AND LEFT(LTRIM(t.[benefits]), 1) = N'[' THEN t.[benefits] END AS [Benefits],
        CONVERT(int, a.[key]) AS [Position]
    FROM [Organizations] o
    JOIN settings s ON s.[OrganizationId] = o.[Id] AND s.[Latest] = 1
    CROSS APPLY OPENJSON(s.[Payload]) root
    CROSS APPLY OPENJSON(CASE WHEN root.[key] = N'tiers' AND root.[type] = 4 THEN root.[value] END) a
    CROSS APPLY OPENJSON(CASE WHEN a.[type] = 5 THEN a.[value] END) WITH (
        [tier] nvarchar(max) '$.tier', [monthlyDues] nvarchar(max) '$.monthlyDues',
        [initiationFee] nvarchar(max) '$.initiationFee', [loyaltyMultiplier] nvarchar(max) '$.loyaltyMultiplier',
        [benefits] nvarchar(max) '$.benefits' AS JSON) t
    WHERE {NoTiersYet} AND LEN(LTRIM(RTRIM(COALESCE(t.[tier], N'')))) > 0
),
firsts AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY [OrganizationId], [Name] ORDER BY [Position]) AS [Seen]
    FROM listed
)
INSERT INTO [ClubCatalogItems] ([Id], [OrganizationId], [Kind], [Name], [Description], [Category], [Code], [Price], [Currency],
    [VatRate], [Cadence], [InitiationFee], [LoyaltyMultiplier], [Benefits], [ChargeKind], [IsActive], [SortOrder],
    [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [UpdatedAtUtc], [UpdatedByUserId], [UpdatedByName], [Version])
SELECT NEWID(), f.[OrganizationId], N'Membership', f.[Name], NULL, NULL, NULL,
    CASE WHEN f.[Dues] >= 0 AND f.[Dues] < 1000000000 THEN ROUND(f.[Dues], 2) ELSE 0 END,
    f.[Currency], 0, N'Monthly',
    CASE WHEN f.[Fee] >= 0 AND f.[Fee] < 1000000000 THEN ROUND(f.[Fee], 2) ELSE 0 END,
    CASE WHEN f.[Multiplier] > 0 AND f.[Multiplier] < 1000 THEN ROUND(f.[Multiplier], 2) ELSE 1 END,
    f.[Benefits], N'Dues', 1, f.[Position],
    @now, NULL, NULL, @now, NULL, NULL, NEWID()
FROM firsts f
WHERE f.[Seen] = 1;

-- Everyone else: the four tiers the settings page has always shown.
INSERT INTO [ClubCatalogItems] ([Id], [OrganizationId], [Kind], [Name], [Description], [Category], [Code], [Price], [Currency],
    [VatRate], [Cadence], [InitiationFee], [LoyaltyMultiplier], [Benefits], [ChargeKind], [IsActive], [SortOrder],
    [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [UpdatedAtUtc], [UpdatedByUserId], [UpdatedByName], [Version])
SELECT NEWID(), o.[Id], N'Membership', v.[Name], NULL, NULL, NULL, v.[Price], {Currency},
    0, N'Monthly', v.[Fee], v.[Multiplier], v.[Benefits], N'Dues', 1, v.[Position],
    @now, NULL, NULL, @now, NULL, NULL, NEWID()
FROM [Organizations] o
CROSS JOIN (VALUES
    (N'Bronze', CAST(49 AS decimal(18,2)), CAST(0 AS decimal(18,2)), CAST(1.00 AS decimal(5,2)),
        N'[""Coworking access"",""Member rate on events"",""Member directory""]', 0),
    (N'Silver', 129, 250, 1.25,
        N'[""All Bronze benefits"",""Spa access"",""Fitness studio"",""Rooftop access""]', 1),
    (N'Gold', 299, 1000, 1.50,
        N'[""All Silver benefits"",""Private dining priority"",""Guest passes (4/mo)"",""Tennis court""]', 2),
    (N'Platinum', 599, 2500, 2.00,
        N'[""All Gold benefits"",""Unlimited guest passes"",""Comp spa treatment monthly"",""Founders events""]', 3)
) v([Name], [Price], [Fee], [Multiplier], [Benefits], [Position])
WHERE {NoTiersYet};";
    }
}
