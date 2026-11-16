using System.Globalization;
using ACI.Application.Services;

namespace ACI.Application.Common;

/// <summary>
/// The referrals report as a spreadsheet: one row per member brought in, with who brought them
/// and what they were charged in the period (dues, everything else, both) per currency, so a
/// committee can sort and pivot it. Members who proposed or seconded but brought nobody in get
/// a row of their own with their counts. Written through <see cref="CsvExport"/>: text that
/// starts with = + - or @ reaches the spreadsheet as text, and amounts stay numbers.
/// </summary>
public static class ReferralReportCsv
{
    public static readonly string[] Headers =
    [
        "Referrer", "Referrer status", "Referred member", "Joined", "Member status", "Tier", "Currency", "Dues", "Spend", "Revenue",
        "Proposed", "Seconded",
    ];

    public static string FileName(ReferralReportDto report) =>
        $"referrals-{report.FromUtc:yyyy-MM-dd}-to-{report.ToUtc.AddTicks(-1):yyyy-MM-dd}.csv";

    /// <param name="separator">The workspace's column separator (<see cref="CsvExport.SeparatorFor"/>).</param>
    public static byte[] Write(ReferralReportDto report, char separator = CsvExport.Comma)
    {
        var csv = new CsvExport(separator, Headers);
        foreach (var referrer in report.Referrers)
        {
            if (referrer.Members.Count == 0)
            {
                csv.Mixed(referrer.Name, referrer.Status, null, null, null, null, null, null, null, null, referrer.Proposed, referrer.Seconded);
                continue;
            }
            foreach (var member in referrer.Members)
            {
                var currencies = member.Dues.Select(a => a.Currency).Concat(member.Spend.Select(a => a.Currency)).Distinct().ToList();
                if (currencies.Count == 0) currencies.Add(report.Currency);
                foreach (var currency in currencies)
                {
                    var dues = member.Dues.Where(a => a.Currency == currency).Sum(a => a.Amount);
                    var spend = member.Spend.Where(a => a.Currency == currency).Sum(a => a.Amount);
                    csv.Mixed(referrer.Name, referrer.Status, member.Name, member.JoinedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        member.Status, member.Tier, currency, Money(dues), Money(spend), Money(dues + spend), referrer.Proposed, referrer.Seconded);
                }
            }
        }
        return csv.ToBytes();
    }

    private static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
