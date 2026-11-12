using System.Globalization;
using System.Text;

namespace ACI.Application.Common.Payments;

/// <summary>
/// A postal address as a QR-bill takes it: structured (address type S), which is the only kind
/// the Swiss Implementation Guidelines for the QR-bill accept from version 2.3.
/// </summary>
/// <param name="Name">The name or company, at most 70 characters.</param>
/// <param name="Street">The street, at most 70; may be left out.</param>
/// <param name="BuildingNumber">The house number, at most 16; may be left out.</param>
/// <param name="PostalCode">At most 16 characters, without a country prefix.</param>
/// <param name="Town">At most 35 characters.</param>
/// <param name="Country">The two-letter ISO 3166-1 country code.</param>
public sealed record QrBillAddress(string Name, string? Street, string? BuildingNumber, string PostalCode, string Town, string Country);

/// <summary>What a QR-bill says: who is paid, how much, by whom, and the reference that says what for.</summary>
/// <param name="Account">The creditor's IBAN or QR-IBAN (CH or LI).</param>
/// <param name="Amount">What is to be paid; null leaves it for the payer to fill in.</param>
/// <param name="Currency">CHF or EUR.</param>
/// <param name="Debtor">Who pays; null leaves a box for the payer to write in.</param>
/// <param name="ReferenceType">QRR (with a QR-IBAN), SCOR or NON.</param>
/// <param name="Message">Free text the payer's bank passes on, such as "Statement October 2026".</param>
/// <param name="BillInformation">Structured billing information (Swico S1); with the message, at most 140 characters.</param>
public sealed record QrBill(
    string Account,
    QrBillAddress Creditor,
    decimal? Amount,
    string Currency,
    QrBillAddress? Debtor,
    string ReferenceType,
    string? Reference,
    string? Message,
    string? BillInformation = null);

/// <summary>
/// The Swiss QR-bill's data, as the Swiss QR Code in its payment part carries it: the "SPC"
/// payload of the SIX Swiss Payment Standards, Implementation Guidelines for the QR-bill,
/// version 2.3 (structured addresses only).
/// </summary>
/// <remarks>
/// <para>
/// The payload is one data element per line, in the order the guidelines fix, separated by a
/// line feed, and ends with the trailer "EPD" unless billing information follows. The ultimate
/// creditor's seven elements are reserved for future use and left empty. Text is cut to the
/// guidelines' lengths and kept to the character set they allow (Latin letters with their
/// accents, and the euro sign); anything else becomes a full stop rather than break the code.
/// </para>
/// <para>
/// <see cref="Validate"/> says what is wrong before anything is printed; a bill that does not
/// validate is not printed at all, as a slip the bank refuses is worse than none.
/// </para>
/// </remarks>
public static class SwissQrBill
{
    /// <summary>The longest payload the guidelines allow, in characters.</summary>
    public const int MaxPayloadLength = 997;

    public const string Qrr = "QRR";
    public const string Scor = "SCOR";
    public const string Non = "NON";

    /// <summary>What is wrong with the bill, in sentences; empty when it can be printed.</summary>
    public static IReadOnlyList<string> Validate(QrBill bill)
    {
        var problems = new List<string>();
        var account = Iban.Normalize(bill.Account);
        if (!Iban.IsSwiss(account)) problems.Add("The account must be a valid Swiss or Liechtenstein IBAN.");
        CheckAddress(bill.Creditor, "The creditor", problems);
        if (bill.Debtor != null) CheckAddress(bill.Debtor, "The debtor", problems);
        if (bill.Amount is { } amount && (amount < 0.01m || amount > 999_999_999.99m || amount != Math.Round(amount, 2)))
            problems.Add("The amount must be from 0.01 to 999,999,999.99, to the cent.");
        if (bill.Currency is not ("CHF" or "EUR")) problems.Add("The currency must be CHF or EUR.");

        var qrIban = Iban.IsQrIban(account);
        var reference = PaymentReferences.Compact(bill.Reference);
        switch (bill.ReferenceType)
        {
            case Qrr:
                if (!qrIban) problems.Add("A QR reference needs a QR-IBAN.");
                if (!PaymentReferences.IsQrReference(reference)) problems.Add("The QR reference must be 27 digits with the right check digit.");
                break;
            case Scor:
                if (qrIban) problems.Add("A QR-IBAN takes a QR reference, not a creditor reference.");
                if (!PaymentReferences.IsCreditorReference(reference)) problems.Add("The creditor reference must be an ISO 11649 reference starting RF.");
                break;
            case Non:
                if (qrIban) problems.Add("A QR-IBAN takes a QR reference.");
                if (reference.Length > 0) problems.Add("A bill without a reference cannot carry one.");
                break;
            default:
                problems.Add("The reference type must be QRR, SCOR or NON.");
                break;
        }
        if (Clean(bill.Message).Length + Clean(bill.BillInformation).Length > 140)
            problems.Add("The message and the billing information together must be at most 140 characters.");
        if (problems.Count == 0 && Payload(bill).Length > MaxPayloadLength)
            problems.Add($"The bill's data is longer than the {MaxPayloadLength} characters a Swiss QR Code holds.");
        return problems;
    }

    /// <summary>The bill as the Swiss QR Code carries it.</summary>
    public static string Payload(QrBill bill)
    {
        var lines = new List<string>
        {
            "SPC",            // QR type
            "0200",           // version 2.0 of the data structure
            "1",              // coding: UTF-8, restricted to the Latin character set
            Iban.Normalize(bill.Account),
        };
        AddAddress(lines, bill.Creditor);
        for (var i = 0; i < 7; i++) lines.Add("");   // the ultimate creditor, for future use
        lines.Add(bill.Amount is { } amount ? amount.ToString("0.00", CultureInfo.InvariantCulture) : "");
        lines.Add(bill.Currency);
        if (bill.Debtor != null) AddAddress(lines, bill.Debtor);
        else for (var i = 0; i < 7; i++) lines.Add("");
        lines.Add(bill.ReferenceType);
        lines.Add(PaymentReferences.Compact(bill.Reference));
        lines.Add(Clean(bill.Message, 140));
        lines.Add("EPD");
        var billInformation = Clean(bill.BillInformation, 140);
        if (billInformation.Length > 0) lines.Add(billInformation);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// The text kept to the character set the guidelines allow, on one line, without spaces at
    /// either end, and cut to <paramref name="max"/> characters.
    /// </summary>
    public static string Clean(string? text, int max = int.MaxValue)
    {
        var builder = new StringBuilder();
        var lastSpace = true;
        foreach (var c in (text ?? "").Normalize(NormalizationForm.FormC))
        {
            var ch = char.IsWhiteSpace(c) ? ' ' : Allowed(c) ? c : '.';
            if (ch == ' ' && lastSpace) continue;
            builder.Append(ch);
            lastSpace = ch == ' ';
        }
        var cleaned = builder.ToString().Trim();
        return cleaned.Length <= max ? cleaned : cleaned[..max].TrimEnd();
    }

    /// <summary>
    /// The Latin character set of the guidelines: printable ASCII, Latin-1 and Latin Extended-A,
    /// the Romanian S and T with comma below, and the euro sign.
    /// </summary>
    private static bool Allowed(char c) =>
        c is >= ' ' and <= '~'
        || c is >= ' ' and <= 'ſ'
        || c is >= 'Ș' and <= 'ț'
        || c == '€';

    private static void AddAddress(List<string> lines, QrBillAddress address)
    {
        lines.Add("S");
        lines.Add(Clean(address.Name, 70));
        lines.Add(Clean(address.Street, 70));
        lines.Add(Clean(address.BuildingNumber, 16));
        lines.Add(Clean(address.PostalCode, 16));
        lines.Add(Clean(address.Town, 35));
        lines.Add(Clean(address.Country, 2).ToUpperInvariant());
    }

    private static void CheckAddress(QrBillAddress address, string who, List<string> problems)
    {
        if (Clean(address.Name).Length == 0) problems.Add($"{who} needs a name.");
        if (Clean(address.Name).Length > 70) problems.Add($"{who}'s name must be at most 70 characters.");
        if (Clean(address.Street).Length > 70) problems.Add($"{who}'s street must be at most 70 characters.");
        if (Clean(address.BuildingNumber).Length > 16) problems.Add($"{who}'s building number must be at most 16 characters.");
        if (Clean(address.PostalCode).Length is 0 or > 16) problems.Add($"{who} needs a postal code of at most 16 characters.");
        if (Clean(address.Town).Length is 0 or > 35) problems.Add($"{who} needs a town of at most 35 characters.");
        var country = Clean(address.Country);
        if (country.Length != 2 || !country.All(char.IsAsciiLetter)) problems.Add($"{who} needs a two-letter country code, such as CH.");
    }
}
