using System.Security.Cryptography;
using ACI.Application.Common;
using ACI.Application.Common.Payments;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Payments;

/// <summary>A workspace's payment settings as the services use them: the secrets readable, the defaults applied.</summary>
public sealed record PaymentConfig(
    Guid OrganizationId,
    string ClubName,
    string Currency,
    string Locale,
    string TimeZone,
    string? StripeKey,
    bool StripeKeyUnreadable,
    string? WebhookSecret,
    bool OfferTwint,
    bool OfferSepaDebit,
    Guid? WebhookKey,
    string? Iban,
    string? QrIban,
    string? QrReferencePrefix,
    QrBillAddress? Creditor,
    AccountingAccounts Accounts)
{
    public bool OnlinePayments => StripeKey != null;

    /// <summary>What Stripe's page offers in <paramref name="currency"/>: a card always, TWINT in CHF, SEPA direct debit in EUR, as the club turned them on.</summary>
    public IReadOnlyList<string> MethodsFor(string currency)
    {
        var methods = new List<string> { "card" };
        if (OfferTwint && currency == "CHF") methods.Add("twint");
        if (OfferSepaDebit && currency == "EUR") methods.Add("sepa_debit");
        return methods;
    }

    /// <summary>What a saved card or mandate can be in this currency: a card, or a SEPA mandate in EUR.</summary>
    public IReadOnlyList<string> SaveableFor(string currency) =>
        OfferSepaDebit && currency == "EUR" ? ["card", "sepa_debit"] : ["card"];

    /// <summary>Why statements carry no QR-bill, or null when they do.</summary>
    public string? QrBillProblem
    {
        get
        {
            if (QrIban == null && Iban == null) return "Add the club's IBAN or QR-IBAN.";
            if (Creditor == null) return "Add the club's name, postal code, town and country: the QR-bill's creditor.";
            if (Currency is not ("CHF" or "EUR")) return $"QR-bills are in CHF or EUR; this workspace's currency is {Currency}.";
            return null;
        }
    }
}

/// <inheritdoc cref="IClubPaymentSettings"/>
/// <remarks>
/// The Stripe key and the webhook signing secret are encrypted with the server's Data
/// Protection key ring (as two-factor secrets are) and never answered back. A key that can no
/// longer be decrypted (the key ring was lost) counts as no key, and the page says so.
/// </remarks>
public sealed class ClubPaymentSettingsService : IClubPaymentSettings
{
    /// <summary>The events the club's Stripe webhook is to send.</summary>
    public static readonly IReadOnlyList<string> WebhookEvents =
    [
        "checkout.session.completed",
        "checkout.session.async_payment_succeeded",
        "checkout.session.async_payment_failed",
        "checkout.session.expired",
        "payment_intent.succeeded",
        "payment_intent.processing",
        "payment_intent.payment_failed",
        "setup_intent.succeeded",
        "charge.refunded",
    ];

    private readonly AppDbContext _db;
    private readonly ISecretProtector _secrets;
    private readonly IEnumerable<IPaymentProvider> _providers;
    private readonly ICurrentUserService _user;
    private readonly EmailSettings _email;
    private readonly ILogger<ClubPaymentSettingsService> _logger;

    public ClubPaymentSettingsService(AppDbContext db, ISecretProtector secrets, IEnumerable<IPaymentProvider> providers,
        ICurrentUserService user, IOptions<EmailSettings> email, ILogger<ClubPaymentSettingsService> logger)
    {
        _db = db;
        _secrets = secrets;
        _providers = providers;
        _user = user;
        _email = email.Value;
        _logger = logger;
    }

    public async Task<PaymentSettingsDto> GetAsync(Guid organizationId, CancellationToken ct = default)
    {
        var row = await RowAsync(organizationId, ct);
        var config = await ConfigAsync(organizationId, ct);
        return ToDto(row, config);
    }

    public async Task<PaymentStatusDto> StatusAsync(Guid organizationId, CancellationToken ct = default)
    {
        var config = await ConfigAsync(organizationId, ct);
        var qrReady = config.QrBillProblem == null;
        return new PaymentStatusDto(
            config.OnlinePayments,
            config.OnlinePayments ? ModeOf(config.StripeKey) : null,
            config.OnlinePayments ? config.MethodsFor(config.Currency) : Array.Empty<string>(),
            config.OnlinePayments && config.WebhookSecret != null,
            qrReady,
            qrReady ? (config.QrIban != null ? SwissQrBill.Qrr : SwissQrBill.Scor) : null,
            config.Currency);
    }

    public async Task<Result<PaymentSettingsDto>> SaveAsync(Guid organizationId, Guid userId, PaymentSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        var row = await RowAsync(organizationId, ct);
        if (row != null && expectedVersion is { } version && version != row.Version) return DomainErrors.Payments.SettingsChanged;

        // The bank and the creditor, each checked as a QR-bill will need it.
        var iban = Empty(request.Iban) ? null : Iban.Normalize(request.Iban);
        if (iban != null && !Iban.IsSwiss(iban)) return DomainErrors.Payments.SettingsInvalid("The IBAN is not a valid Swiss or Liechtenstein IBAN: check its digits.");
        if (iban != null && Iban.IsQrIban(iban)) return DomainErrors.Payments.SettingsInvalid("That is a QR-IBAN: enter it as the QR-IBAN, and the account's plain IBAN as the IBAN.");
        var qrIban = Empty(request.QrIban) ? null : Iban.Normalize(request.QrIban);
        if (qrIban != null && !Iban.IsQrIban(qrIban)) return DomainErrors.Payments.SettingsInvalid("The QR-IBAN is not a valid QR-IBAN: its bank number (the 5th to 9th characters) is from 30000 to 31999.");
        var prefix = Empty(request.QrReferencePrefix) ? null : request.QrReferencePrefix!.Trim();
        if (prefix != null && (prefix.Length > 12 || !prefix.All(char.IsAsciiDigit)))
            return DomainErrors.Payments.SettingsInvalid("The QR reference prefix is up to 12 digits, as your bank gave it.");
        var creditor = new QrBillAddress(
            Clean(request.CreditorName), Clean(request.CreditorStreet), Clean(request.CreditorBuildingNumber),
            Clean(request.CreditorPostalCode), Clean(request.CreditorTown), Clean(request.CreditorCountry).ToUpperInvariant());
        var anyCreditor = new[] { creditor.Name, creditor.Street, creditor.BuildingNumber, creditor.PostalCode, creditor.Town, creditor.Country }.Any(v => v.Length > 0);
        if (iban != null || qrIban != null || anyCreditor)
        {
            if (creditor.Name.Length is 0 or > 70) return DomainErrors.Payments.SettingsInvalid("The creditor's name is needed, at most 70 characters: the club as the bank knows it.");
            if (creditor.Street!.Length > 70) return DomainErrors.Payments.SettingsInvalid("The creditor's street is at most 70 characters.");
            if (creditor.BuildingNumber!.Length > 16) return DomainErrors.Payments.SettingsInvalid("The creditor's building number is at most 16 characters.");
            if (creditor.PostalCode.Length is 0 or > 16) return DomainErrors.Payments.SettingsInvalid("The creditor's postal code is needed, at most 16 characters.");
            if (creditor.Town.Length is 0 or > 35) return DomainErrors.Payments.SettingsInvalid("The creditor's town is needed, at most 35 characters.");
            if (creditor.Country.Length != 2 || !creditor.Country.All(char.IsAsciiLetterUpper))
                return DomainErrors.Payments.SettingsInvalid("The creditor's country is a two-letter code, such as CH.");
        }

        var stored = row != null ? AccountingAccounts.Read(row.AccountsJson) : AccountingAccounts.Default;
        var accounts = request.Accounts == null ? stored : stored.Over(request.Accounts);
        if (accounts.Problem() is { } accountProblem) return DomainErrors.Payments.SettingsInvalid(accountProblem);

        // A new Stripe key is tried with Stripe before it is kept, so a typo shows now rather
        // than when a member presses Pay.
        string? newKey = null;
        if (request.StripeSecretKey is { } keyText && keyText.Trim().Length > 0)
        {
            newKey = keyText.Trim();
            if (newKey.StartsWith("pk_", StringComparison.Ordinal))
                return DomainErrors.Payments.SettingsInvalid("That is the publishable key. Paste the secret key (sk_…) or a restricted key (rk_…).");
            if (ModeOf(newKey) == null || newKey.Length < 20 || newKey.Any(char.IsWhiteSpace))
                return DomainErrors.Payments.SettingsInvalid("A Stripe secret key starts sk_live_, sk_test_, rk_live_ or rk_test_.");
            var check = await Stripe().CheckKeyAsync(newKey, ct);
            if (check.Failure is { } failure)
                return failure.Unreachable
                    ? DomainErrors.Payments.ProviderUnreachable("Stripe could not be reached to check the key. Try again in a minute.")
                    : DomainErrors.Payments.SettingsInvalid($"Stripe did not accept the key: {failure.Message}");
        }
        string? newSecret = null;
        if (request.StripeWebhookSecret is { } secretText && secretText.Trim().Length > 0)
        {
            newSecret = secretText.Trim();
            if (!newSecret.StartsWith("whsec_", StringComparison.Ordinal) || newSecret.Any(char.IsWhiteSpace))
                return DomainErrors.Payments.SettingsInvalid("The webhook signing secret starts whsec_: copy it from the webhook endpoint in Stripe.");
        }

        var now = DateTime.UtcNow;
        if (row == null)
        {
            row = new ClubPaymentSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                WebhookKey = Guid.NewGuid(),
                CreatedAtUtc = now,
            };
            _db.ClubPaymentSettings.Add(row);
        }
        if (newKey != null)
        {
            row.StripeSecretKeyProtected = _secrets.Protect(newKey);
            row.StripeKeyHint = Hint(newKey);
            row.StripeCheckedAtUtc = now;
        }
        else if (request.StripeSecretKey != null)
        {
            // Sent empty: the key is taken away, and online payments stop.
            row.StripeSecretKeyProtected = null;
            row.StripeKeyHint = null;
            row.StripeCheckedAtUtc = null;
        }
        if (newSecret != null) row.StripeWebhookSecretProtected = _secrets.Protect(newSecret);
        else if (request.StripeWebhookSecret != null) row.StripeWebhookSecretProtected = null;
        if (request.StripeOfferTwint is { } twint) row.StripeOfferTwint = twint;
        if (request.StripeOfferSepaDebit is { } sepa) row.StripeOfferSepaDebit = sepa;
        row.Iban = iban;
        row.QrIban = qrIban;
        row.QrReferencePrefix = prefix;
        row.CreditorName = Null(creditor.Name);
        row.CreditorStreet = Null(creditor.Street);
        row.CreditorBuildingNumber = Null(creditor.BuildingNumber);
        row.CreditorPostalCode = Null(creditor.PostalCode);
        row.CreditorTown = Null(creditor.Town);
        row.CreditorCountry = Null(creditor.Country);
        row.AccountsJson = accounts.ToJson();
        row.UpdatedAtUtc = now;
        row.UpdatedByUserId = userId;
        row.UpdatedByName = string.IsNullOrWhiteSpace(_user.UserName) ? null : _user.UserName.Trim()[..Math.Min(200, _user.UserName.Trim().Length)];
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DomainErrors.Payments.SettingsChanged;
        }
        catch (DbUpdateException)
        {
            // Two first saves at once: the other made the workspace's row.
            return DomainErrors.Payments.SettingsChanged;
        }
        return await GetAsync(organizationId, ct);
    }

    /// <summary>The workspace's settings as the services use them, the secrets decrypted.</summary>
    public async Task<PaymentConfig> ConfigAsync(Guid organizationId, CancellationToken ct = default)
    {
        var row = await RowAsync(organizationId, ct);
        // Organizations are not filtered by workspace: this is the one asked for.
        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
            .Select(o => new { o.Name, o.Currency, o.Locale, o.TimeZone }).FirstOrDefaultAsync(ct);
        var currency = RegionalSettings.TryCurrency(org?.Currency, out var code) ? code : RegionalSettings.DefaultCurrency;
        var key = Reveal(row?.StripeSecretKeyProtected, out var keyUnreadable);
        var secret = Reveal(row?.StripeWebhookSecretProtected, out _);
        QrBillAddress? creditor = null;
        if (!string.IsNullOrWhiteSpace(row?.CreditorName) && !string.IsNullOrWhiteSpace(row.CreditorPostalCode)
            && !string.IsNullOrWhiteSpace(row.CreditorTown) && !string.IsNullOrWhiteSpace(row.CreditorCountry))
            creditor = new QrBillAddress(row.CreditorName, row.CreditorStreet, row.CreditorBuildingNumber, row.CreditorPostalCode, row.CreditorTown, row.CreditorCountry);
        return new PaymentConfig(
            organizationId,
            org?.Name ?? "",
            currency,
            string.IsNullOrWhiteSpace(org?.Locale) ? RegionalSettings.DefaultLocale : org.Locale,
            string.IsNullOrWhiteSpace(org?.TimeZone) ? RegionalSettings.DefaultTimeZone : org.TimeZone,
            key,
            keyUnreadable,
            secret,
            row?.StripeOfferTwint ?? false,
            row?.StripeOfferSepaDebit ?? false,
            row?.WebhookKey,
            row?.Iban,
            row?.QrIban,
            row?.QrReferencePrefix,
            creditor,
            AccountingAccounts.Read(row?.AccountsJson));
    }

    /// <summary>The webhook address Stripe posts a workspace's events to.</summary>
    public string WebhookUrl(Guid webhookKey) =>
        $"{(_email.ApiBaseUrl ?? "").TrimEnd('/')}/api/public/payments/stripe/{webhookKey:N}";

    private IPaymentProvider Stripe() =>
        _providers.First(p => p.Name == PaymentProviders.Stripe);

    private async Task<ClubPaymentSettings?> RowAsync(Guid organizationId, CancellationToken ct) =>
        await _db.ClubPaymentSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

    private string? Reveal(string? protectedValue, out bool unreadable)
    {
        unreadable = false;
        if (string.IsNullOrEmpty(protectedValue)) return null;
        try
        {
            return _secrets.Unprotect(protectedValue);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "A saved payment secret could not be decrypted; the Data Protection key ring may have changed");
            unreadable = true;
            return null;
        }
    }

    private PaymentSettingsDto ToDto(ClubPaymentSettings? row, PaymentConfig config) => new()
    {
        StripeConfigured = config.OnlinePayments,
        StripeKeyHint = config.OnlinePayments ? row?.StripeKeyHint : config.StripeKeyUnreadable ? "The saved key can no longer be read: enter it again" : null,
        StripeMode = config.OnlinePayments ? ModeOf(config.StripeKey) : null,
        StripeWebhookSecretSet = config.WebhookSecret != null,
        StripeOfferTwint = config.OfferTwint,
        StripeOfferSepaDebit = config.OfferSepaDebit,
        StripeCheckedAtUtc = row?.StripeCheckedAtUtc,
        WebhookUrl = row != null ? WebhookUrl(row.WebhookKey) : "",
        WebhookEvents = WebhookEvents,
        Iban = row?.Iban,
        QrIban = row?.QrIban,
        QrReferencePrefix = row?.QrReferencePrefix,
        CreditorName = row?.CreditorName,
        CreditorStreet = row?.CreditorStreet,
        CreditorBuildingNumber = row?.CreditorBuildingNumber,
        CreditorPostalCode = row?.CreditorPostalCode,
        CreditorTown = row?.CreditorTown,
        CreditorCountry = row?.CreditorCountry,
        QrBillReady = config.QrBillProblem == null,
        QrBillProblem = config.QrBillProblem,
        Accounts = config.Accounts,
        Currency = config.Currency,
        UpdatedAtUtc = row?.UpdatedAtUtc,
        UpdatedByName = row?.UpdatedByName,
        Version = row?.Version ?? Guid.Empty,
    };

    /// <summary>test or live from a Stripe secret or restricted key; null for anything else.</summary>
    public static string? ModeOf(string? key) => key switch
    {
        null => null,
        _ when key.StartsWith("sk_live_", StringComparison.Ordinal) || key.StartsWith("rk_live_", StringComparison.Ordinal) => "live",
        _ when key.StartsWith("sk_test_", StringComparison.Ordinal) || key.StartsWith("rk_test_", StringComparison.Ordinal) => "test",
        _ => null,
    };

    /// <summary>What a screen shows of a key: its kind and mode, and its last four characters.</summary>
    private static string Hint(string key) => $"{key[..8]}…{key[^4..]}";

    private static bool Empty(string? value) => string.IsNullOrWhiteSpace(value);
    private static string Clean(string? value) => SwissQrBill.Clean(value);
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
