namespace ACI.WebApi;

/// <summary>
/// The unique indexes that keep a contact email to one active contact per workspace:
/// per organization, and with none, per user. Run at startup (see Program.cs).
/// </summary>
/// <remarks>
/// <para>
/// They replace InitialCreate's one index over (Email, OrganizationId), which counted
/// every personal user's NULL as the same workspace, so a second personal user could
/// not add a contact the first one had. The new ones go in before the old one is
/// dropped.
/// </para>
/// <para>
/// Each one goes in only when the rows already meet it. A database whose InitialCreate
/// was marked applied rather than run never had the old index, so nothing stopped two
/// contacts in one organization sharing an email. On such a database CREATE UNIQUE
/// INDEX fails with a duplicate key, and it failed inside the one catch that wraps all
/// the startup schema work, so everything after it (later tables, migrations, seeding)
/// stopped on every start. Without the index the services still refuse a duplicate;
/// <see cref="CountSql"/> lets startup say that an index is missing and why.
/// </para>
/// </remarks>
public static class ContactEmailIndexes
{
    public const string OrganizationIndex = "IX_Contacts_Email_Organization";
    public const string PersonalIndex = "IX_Contacts_Email_PersonalUser";

    public const string Sql = @"
        IF OBJECT_ID(N'dbo.Contacts') IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Contacts') AND name = '" + OrganizationIndex + @"')
                AND NOT EXISTS (SELECT 1 FROM [Contacts] WHERE [OrganizationId] IS NOT NULL AND [IsArchived] = 0
                    GROUP BY [Email], [OrganizationId] HAVING COUNT(*) > 1)
                CREATE UNIQUE INDEX [" + OrganizationIndex + @"] ON [Contacts] ([Email], [OrganizationId])
                    WHERE [OrganizationId] IS NOT NULL AND [IsArchived] = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Contacts') AND name = '" + PersonalIndex + @"')
                AND NOT EXISTS (SELECT 1 FROM [Contacts] WHERE [OrganizationId] IS NULL AND [IsArchived] = 0
                    GROUP BY [Email], [UserId] HAVING COUNT(*) > 1)
                CREATE UNIQUE INDEX [" + PersonalIndex + @"] ON [Contacts] ([Email], [UserId])
                    WHERE [OrganizationId] IS NULL AND [IsArchived] = 0;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Contacts') AND name = 'IX_Contacts_Email_OrganizationId')
                AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Contacts') AND name = '" + OrganizationIndex + @"')
                AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Contacts') AND name = '" + PersonalIndex + @"')
                DROP INDEX [IX_Contacts_Email_OrganizationId] ON [Contacts];
        END;";

    /// <summary>How many of the two indexes are in place, as the single column EF reads back.</summary>
    public const string CountSql = @"
        SELECT COUNT(*) AS [Value] FROM sys.indexes
        WHERE object_id = OBJECT_ID('Contacts') AND name IN ('" + OrganizationIndex + "', '" + PersonalIndex + "')";
}
