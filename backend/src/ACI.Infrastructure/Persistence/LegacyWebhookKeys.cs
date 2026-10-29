namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Moves lead webhook keys saved in plain text onto their hash, in place, at start-up.
/// </summary>
/// <remarks>
/// <para>
/// The hash must be the one <c>OrganizationService.HashApiKey</c> computes, or every
/// sender using an existing key is refused: lower-case hex SHA-256 of the key's UTF-8
/// bytes. The keys this app generated are "aci_" plus base64url, all ASCII, so their
/// varchar bytes are their UTF-8 bytes, and CONVERT style 2 is hex without "0x".
/// </para>
/// <para>
/// Idempotent: it touches only rows that still hold a plain key, and clears that key,
/// so a second run finds nothing to do.
/// </para>
/// </remarks>
public static class LegacyWebhookKeys
{
    public const string HashInPlaceSql = @"
        UPDATE [Organizations]
        SET [WebhookApiKeyHash] = LOWER(CONVERT(nvarchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(max), [WebhookApiKey])), 2)),
            [WebhookApiKey] = NULL
        WHERE [WebhookApiKey] IS NOT NULL AND [WebhookApiKey] <> N'';
    ";
}
