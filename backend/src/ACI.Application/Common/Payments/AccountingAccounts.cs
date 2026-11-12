using System.Text.Json;

namespace ACI.Application.Common.Payments;

/// <summary>
/// The accounts the accounting journal books to, by what each entry is. The defaults are the
/// Swiss SME chart of accounts (KMU-Kontenrahmen); a club changes any it books differently.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>1000 Kasse (cash), 1020 Bankguthaben (bank), 1090 Transferkonto (money on its way:
/// card terminal, TWINT and Stripe payouts), 1100 Forderungen aus Lieferungen und Leistungen
/// (receivables).</item>
/// <item>2030 Erhaltene Anzahlungen (customer prepayments): what members paid beyond what they
/// owe, and, as the chart has no account of its own for them, gift cards sold and not yet
/// spent. A club that keeps gift cards apart sets an account of its own (say 2035).</item>
/// <item>2200 Geschuldete MWST (VAT owed).</item>
/// <item>Revenue: 3200 Handelserlöse for food, drink and goods; 3400 Dienstleistungserlöse
/// for dues, events, spa and bookings; 3600 Übrige Erlöse for the rest, corrections and gift
/// cards spent or expired; 3800 Erlösminderungen for credits given by hand.</item>
/// <item>9100 Eröffnungsbilanz for balances brought over from before the ledger.</item>
/// </list>
/// </remarks>
public sealed record AccountingAccounts
{
    public string Cash { get; init; } = "1000";
    public string Bank { get; init; } = "1020";
    public string CardClearing { get; init; } = "1090";
    public string Receivables { get; init; } = "1100";
    public string CustomerPrepayments { get; init; } = "2030";
    public string GiftCardLiability { get; init; } = "2030";
    public string VatOwed { get; init; } = "2200";
    public string OpeningBalances { get; init; } = "9100";

    /// <summary>A balance set higher by hand: owed more, booked as other revenue.</summary>
    public string Corrections { get; init; } = "3600";

    /// <summary>A balance set lower by hand: a credit, booked as a sales deduction.</summary>
    public string CreditCorrections { get; init; } = "3800";

    /// <summary>Where the money for a gift card sold goes: the ledger records no payment for the sale, so cash by default.</summary>
    public string GiftCardSales { get; init; } = "1000";

    public string GiftCardRedemptions { get; init; } = "3600";
    public string GiftCardBreakage { get; init; } = "3600";

    /// <summary>Revenue by charge kind (Dues, Dining, Bar, Event, Spa, Booking, Retail, Other).</summary>
    public IReadOnlyDictionary<string, string> RevenueByKind { get; init; } = DefaultRevenue;

    /// <summary>The accounting software's VAT code for each Swiss rate ("8.1", "2.6", "3.8"); empty until the club sets them.</summary>
    public IReadOnlyDictionary<string, string> VatCodes { get; init; } = DefaultVatCodes;

    public static readonly IReadOnlyDictionary<string, string> DefaultRevenue = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Dues"] = "3400", ["Dining"] = "3200", ["Bar"] = "3200", ["Event"] = "3400",
        ["Spa"] = "3400", ["Booking"] = "3400", ["Retail"] = "3200", ["Other"] = "3600",
    };

    public static readonly IReadOnlyDictionary<string, string> DefaultVatCodes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["8.1"] = "", ["2.6"] = "", ["3.8"] = "",
    };

    public static readonly AccountingAccounts Default = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The revenue account for a charge of this kind.</summary>
    public string RevenueFor(string? kind) =>
        kind != null && RevenueByKind.TryGetValue(kind, out var account) && account.Length > 0 ? account : RevenueByKind.GetValueOrDefault("Other") ?? "3600";

    /// <summary>The VAT code for a rate, or empty when the club has not set one.</summary>
    public string VatCodeFor(decimal? rate) =>
        rate is { } r && VatCodes.TryGetValue(r.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture), out var code) ? code : "";

    /// <summary>The stored accounts over the defaults: an account left out or blank keeps the chart's.</summary>
    public static AccountingAccounts Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default;
        try
        {
            var stored = JsonSerializer.Deserialize<AccountingAccountsRequest>(json, Json);
            return stored == null ? Default : Default.Over(stored);
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>These accounts with <paramref name="changes"/> laid over them: an account left out or blank stays as it was.</summary>
    public AccountingAccounts Over(AccountingAccountsRequest changes)
    {
        static string Pick(string mine, string? theirs) => string.IsNullOrWhiteSpace(theirs) ? mine : theirs.Trim();
        var revenue = new Dictionary<string, string>(RevenueByKind, StringComparer.Ordinal);
        foreach (var (kind, account) in changes.RevenueByKind ?? new Dictionary<string, string>())
            if (revenue.ContainsKey(kind) && !string.IsNullOrWhiteSpace(account)) revenue[kind] = account.Trim();
        var vat = new Dictionary<string, string>(VatCodes, StringComparer.Ordinal);
        foreach (var (rate, code) in changes.VatCodes ?? new Dictionary<string, string>())
            if (vat.ContainsKey(rate)) vat[rate] = (code ?? "").Trim();
        return new AccountingAccounts
        {
            Cash = Pick(Cash, changes.Cash),
            Bank = Pick(Bank, changes.Bank),
            CardClearing = Pick(CardClearing, changes.CardClearing),
            Receivables = Pick(Receivables, changes.Receivables),
            CustomerPrepayments = Pick(CustomerPrepayments, changes.CustomerPrepayments),
            GiftCardLiability = Pick(GiftCardLiability, changes.GiftCardLiability),
            VatOwed = Pick(VatOwed, changes.VatOwed),
            OpeningBalances = Pick(OpeningBalances, changes.OpeningBalances),
            Corrections = Pick(Corrections, changes.Corrections),
            CreditCorrections = Pick(CreditCorrections, changes.CreditCorrections),
            GiftCardSales = Pick(GiftCardSales, changes.GiftCardSales),
            GiftCardRedemptions = Pick(GiftCardRedemptions, changes.GiftCardRedemptions),
            GiftCardBreakage = Pick(GiftCardBreakage, changes.GiftCardBreakage),
            RevenueByKind = revenue,
            VatCodes = vat,
        };
    }

    /// <summary>What is wrong with the accounts, as a sentence; null when every one is an account number.</summary>
    public string? Problem()
    {
        var accounts = new (string Name, string Value)[]
        {
            ("cash", Cash), ("bank", Bank), ("card and online payments clearing", CardClearing), ("receivables", Receivables),
            ("customer prepayments", CustomerPrepayments), ("gift card liability", GiftCardLiability), ("VAT owed", VatOwed),
            ("opening balances", OpeningBalances), ("corrections", Corrections), ("credit corrections", CreditCorrections),
            ("gift card sales", GiftCardSales), ("gift cards spent", GiftCardRedemptions), ("gift cards expired", GiftCardBreakage),
        }.Concat(RevenueByKind.Select(r => ($"{r.Key} revenue", r.Value)));
        foreach (var (name, value) in accounts)
            if (!IsAccount(value)) return $"The {name} account must be 1 to 10 letters, digits, dots or dashes, such as 1020.";
        foreach (var (rate, code) in VatCodes)
            if (code.Length > 20 || code.Any(char.IsControl)) return $"The VAT code for {rate}% must be at most 20 characters.";
        return null;
    }

    private static bool IsAccount(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 10 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-');
}

/// <summary>Accounts to change: one left out (null) or blank stays as it was.</summary>
public sealed class AccountingAccountsRequest
{
    public string? Cash { get; set; }
    public string? Bank { get; set; }
    public string? CardClearing { get; set; }
    public string? Receivables { get; set; }
    public string? CustomerPrepayments { get; set; }
    public string? GiftCardLiability { get; set; }
    public string? VatOwed { get; set; }
    public string? OpeningBalances { get; set; }
    public string? Corrections { get; set; }
    public string? CreditCorrections { get; set; }
    public string? GiftCardSales { get; set; }
    public string? GiftCardRedemptions { get; set; }
    public string? GiftCardBreakage { get; set; }
    public Dictionary<string, string>? RevenueByKind { get; set; }
    public Dictionary<string, string>? VatCodes { get; set; }
}
