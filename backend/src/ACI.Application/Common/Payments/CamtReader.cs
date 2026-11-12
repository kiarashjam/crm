using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ACI.Application.Common.Payments;

/// <summary>One booking a bank file reports, as a credit or a debit on the club's account.</summary>
/// <param name="BankReference">
/// The bank's own reference for it, with the account: what makes it once only. The account
/// servicer's reference of the transaction, else the entry's with the transaction's place in
/// it, else a hash of what the bank reported, which a file repeating it gives again.
/// </param>
/// <param name="Status">BOOK (booked), PDNG (pending) or INFO.</param>
/// <param name="ReferenceType">QRR or SCOR when the payer quoted a structured reference.</param>
public sealed record CamtTransaction(
    string BankReference,
    string? AccountIban,
    DateTime BookedAtUtc,
    DateTime? ValueDateUtc,
    decimal Amount,
    string Currency,
    bool IsCredit,
    bool IsReversal,
    string Status,
    string? ReferenceType,
    string? Reference,
    string? DebtorName,
    string? DebtorIban,
    string? Message,
    string? EndToEndId);

/// <summary>A bank file: which camt message it is, its id, the accounts in it and every booking.</summary>
public sealed record CamtFile(string Format, string? MessageId, IReadOnlyList<string> Accounts, IReadOnlyList<CamtTransaction> Transactions);

/// <summary>Why a file could not be read, in a sentence for the person who uploaded it.</summary>
public sealed class CamtException : Exception
{
    public CamtException(string message) : base(message) { }
}

/// <summary>
/// Reads the ISO 20022 bank-to-customer messages Swiss banks send for an account: camt.054
/// (debit and credit notifications, where QR-bill payments arrive with their reference),
/// camt.053 (the statement) and camt.052 (the intraday report), in any of their versions.
/// </summary>
/// <remarks>
/// <para>
/// Elements are found by their local names, so the versions' different namespaces (camt.054
/// .001.04 of the old Swiss standard, .001.08 of the 2019 one) read alike. A batch entry
/// is read as its transactions (TxDtls), each with its own amount and reference, which is
/// how a bank reports a day's QR-bill payments.
/// </para>
/// <para>
/// The file comes from someone's computer, so no DTD and no outside entity is ever read
/// (an XML bomb or a request to another server stops the read).
/// </para>
/// </remarks>
public static partial class CamtReader
{
    /// <summary>Larger than any day's file a club receives.</summary>
    public const int MaxCharacters = 50_000_000;

    public static CamtFile Read(Stream content)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxCharacters,
                IgnoreComments = true,
            };
            using var reader = XmlReader.Create(content, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            throw new CamtException("The file is not a camt XML file from a bank.");
        }

        var root = document.Root;
        if (root == null || root.Name.LocalName != "Document")
            throw new CamtException("The file is not a camt.054, camt.053 or camt.052 message.");
        var message = root.Elements().FirstOrDefault();
        var (format, itemName) = message?.Name.LocalName switch
        {
            "BkToCstmrDbtCdtNtfctn" => ("camt.054", "Ntfctn"),
            "BkToCstmrStmt" => ("camt.053", "Stmt"),
            "BkToCstmrAcctRpt" => ("camt.052", "Rpt"),
            _ => throw new CamtException("The file is not a camt.054, camt.053 or camt.052 message."),
        };

        var messageId = Value(message!, "GrpHdr", "MsgId");
        var accounts = new List<string>();
        var transactions = new List<CamtTransaction>();
        foreach (var item in Children(message!, itemName))
        {
            var account = Value(item, "Acct", "Id", "IBAN") ?? Value(item, "Acct", "Id", "Othr", "Id");
            if (account != null)
            {
                account = Iban.Normalize(account);
                if (!accounts.Contains(account)) accounts.Add(account);
            }
            foreach (var entry in Children(item, "Ntry"))
                transactions.AddRange(ReadEntry(entry, account));
        }
        return new CamtFile(format, messageId, accounts, transactions);
    }

    private static IEnumerable<CamtTransaction> ReadEntry(XElement entry, string? account)
    {
        var entryAmount = Amount(entry.Elements().FirstOrDefault(e => e.Name.LocalName == "Amt"));
        var entryCredit = Value(entry, "CdtDbtInd") != "DBIT";
        var reversal = string.Equals(Value(entry, "RvslInd"), "true", StringComparison.OrdinalIgnoreCase);
        // Sts is text in the older versions and holds a Cd in the 2019 ones.
        var statusElement = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "Sts");
        var status = (statusElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "Cd")?.Value ?? statusElement?.Value ?? "BOOK").Trim();
        var booked = Date(entry, "BookgDt") ?? Date(entry, "ValDt") ?? DateTime.UtcNow.Date;
        var valued = Date(entry, "ValDt");
        var entryReference = Value(entry, "AcctSvcrRef");
        var entryRef = Value(entry, "NtryRef");
        var additional = Value(entry, "AddtlNtryInf");

        var details = Children(entry, "NtryDtls").SelectMany(d => Children(d, "TxDtls")).ToList();
        if (details.Count == 0)
        {
            var (type, reference) = FindReference(null, [additional]);
            yield return new CamtTransaction(
                Key(account, null, entryReference, entryRef, 0, 1, booked, entryAmount, reference, null, null),
                account, booked, valued, entryAmount.Value, entryAmount.Currency, entryCredit, reversal, status,
                type, reference, null, null, Clip(additional, 500), null);
            yield break;
        }

        for (var i = 0; i < details.Count; i++)
        {
            var tx = details[i];
            var own = Amount(tx.Elements().FirstOrDefault(e => e.Name.LocalName == "Amt"));
            var detailed = Amount(Descendant(tx, "AmtDtls", "TxAmt", "Amt"));
            (decimal Value, string Currency) amount = own.Value > 0 ? own : detailed.Value > 0 ? detailed : details.Count == 1 ? entryAmount : (0m, entryAmount.Currency);
            var credit = Value(tx, "CdtDbtInd") is { } indicator ? indicator != "DBIT" : entryCredit;
            var debtorName = Value(tx, "RltdPties", "Dbtr", "Nm") ?? Value(tx, "RltdPties", "Dbtr", "Pty", "Nm")
                             ?? Value(tx, "RltdPties", "UltmtDbtr", "Nm") ?? Value(tx, "RltdPties", "UltmtDbtr", "Pty", "Nm");
            var debtorIban = Value(tx, "RltdPties", "DbtrAcct", "Id", "IBAN");
            var endToEnd = Value(tx, "Refs", "EndToEndId");
            var remittance = tx.Elements().FirstOrDefault(e => e.Name.LocalName == "RmtInf");
            var unstructured = remittance == null ? new List<string?>() : Children(remittance, "Ustrd").Select(u => (string?)u.Value).ToList();
            var (type, reference) = FindReference(remittance, unstructured.Append(additional).ToList());
            var message = string.Join(" ", unstructured.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!.Trim()));
            yield return new CamtTransaction(
                Key(account, Value(tx, "Refs", "AcctSvcrRef"), entryReference, entryRef, i, details.Count, booked, amount, reference, endToEnd, debtorName),
                account, booked, valued, amount.Value, amount.Currency, credit, reversal, status,
                type, reference, Clip(debtorName, 140), debtorIban is null ? null : Iban.Normalize(debtorIban),
                Clip(message.Length == 0 ? null : message, 500), endToEnd == "NOTPROVIDED" ? null : endToEnd);
        }
    }

    /// <summary>
    /// The structured reference the payer quoted (QRR or SCOR), else one written in the free text,
    /// as some banks pass a creditor reference on.
    /// </summary>
    private static (string? Type, string? Reference) FindReference(XElement? remittance, IReadOnlyList<string?> text)
    {
        var creditorReference = remittance == null ? null : Descendant(remittance, "Strd", "CdtrRefInf");
        if (creditorReference != null)
        {
            var reference = PaymentReferences.Compact(Value(creditorReference, "Ref"));
            var type = (Value(creditorReference, "Tp", "CdOrPrtry", "Cd") ?? Value(creditorReference, "Tp", "CdOrPrtry", "Prtry"))?.Trim().ToUpperInvariant();
            if (reference.Length > 0)
            {
                if (type == "SCOR" || (type == null && PaymentReferences.IsCreditorReference(reference))) return ("SCOR", reference);
                if (type == "QRR" || (type == null && PaymentReferences.IsQrReference(reference))) return ("QRR", reference);
                return (type, reference);
            }
        }
        foreach (var line in text)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            foreach (Match match in CreditorReferencePattern().Matches(line.ToUpperInvariant()))
            {
                // Words written after the reference ("RF18 5390 … thanks") run into the match:
                // the longest start of it whose check digits are right is the reference.
                var candidate = PaymentReferences.Compact(match.Value);
                for (var length = candidate.Length; length >= 5; length--)
                    if (PaymentReferences.IsCreditorReference(candidate[..length])) return ("SCOR", candidate[..length]);
            }
            foreach (Match match in QrReferencePattern().Matches(line))
            {
                var candidate = PaymentReferences.Compact(match.Value);
                if (PaymentReferences.IsQrReference(candidate)) return ("QRR", candidate);
            }
        }
        return (null, null);
    }

    /// <summary>The bank's reference for one transaction of an entry, with the account it is on.</summary>
    private static string Key(string? account, string? transactionReference, string? entryReference, string? entryRef, int index, int count,
        DateTime booked, (decimal Value, string Currency) amount, string? reference, string? endToEnd, string? debtor)
    {
        var prefix = (account ?? "") + ":";
        string key;
        if (!string.IsNullOrWhiteSpace(transactionReference)) key = prefix + transactionReference.Trim();
        else if (!string.IsNullOrWhiteSpace(entryReference)) key = prefix + entryReference.Trim() + (count > 1 ? $"/{index + 1}" : "");
        else if (!string.IsNullOrWhiteSpace(entryRef)) key = prefix + "N:" + entryRef.Trim() + (count > 1 ? $"/{index + 1}" : "");
        else key = prefix + "H:" + Hash($"{booked:yyyy-MM-dd}|{amount.Value.ToString(CultureInfo.InvariantCulture)}|{amount.Currency}|{reference}|{endToEnd}|{debtor}|{index}");
        // The column holds 100 characters; a longer reference is kept as its hash, which is as unique.
        return key.Length <= 95 ? key : prefix + "H:" + Hash(key);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32].ToLowerInvariant();

    private static (decimal Value, string Currency) Amount(XElement? element)
    {
        if (element == null) return (0m, "");
        var currency = (element.Attribute("Ccy")?.Value ?? "").Trim().ToUpperInvariant();
        return decimal.TryParse(element.Value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? (Math.Round(value, 2, MidpointRounding.AwayFromZero), currency)
            : (0m, currency);
    }

    /// <summary>A date element's Dt (a day, taken as midnight UTC) or DtTm (a moment, taken to UTC).</summary>
    private static DateTime? Date(XElement parent, string name)
    {
        var element = parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        if (element == null) return null;
        var day = Value(element, "Dt");
        if (day != null && DateTime.TryParseExact(day.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        var moment = Value(element, "DtTm");
        if (moment != null && DateTimeOffset.TryParse(moment.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
            return at.UtcDateTime;
        return null;
    }

    private static IEnumerable<XElement> Children(XElement parent, string name) =>
        parent.Elements().Where(e => e.Name.LocalName == name);

    private static XElement? Descendant(XElement parent, params string[] path)
    {
        var current = parent;
        foreach (var name in path)
        {
            current = current.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            if (current == null) return null;
        }
        return current;
    }

    private static string? Value(XElement parent, params string[] path)
    {
        var value = Descendant(parent, path)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Clip(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Length <= max ? text.Trim() : text.Trim()[..max];

    [GeneratedRegex(@"RF\s?\d{2}(\s?[A-Z0-9]){1,21}")]
    private static partial Regex CreditorReferencePattern();

    [GeneratedRegex(@"\b\d{2}(\s?\d{5}){5}\b|\b\d{27}\b")]
    private static partial Regex QrReferencePattern();
}
