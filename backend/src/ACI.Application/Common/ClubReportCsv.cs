using System.Globalization;
using ACI.Application.Services;

namespace ACI.Application.Common;

/// <summary>
/// Club Reports as a spreadsheet: one figure per row, so a board pack can filter and pivot it.
/// Written through <see cref="CsvExport"/>: text that starts with = + - or @ reaches the
/// spreadsheet as text, never as a formula, and amounts stay numbers (a negative net too).
/// </summary>
public static class ClubReportCsv
{
    public static readonly string[] Headers = ["Figure", "Period", "Currency", "Detail", "Amount", "Count"];

    /// <summary>
    /// The file's name, with the report's first and last day on the workspace's calendar, as
    /// its rows give them. Read in UTC, a month in Zurich was named from the last day of the
    /// month before, its midnight being the evening before in UTC.
    /// </summary>
    public static string FileName(ClubReportDto report)
    {
        var zone = RegionalSettings.ZoneOf(report.TimeZone);
        return $"club-report-{Day(report.FromUtc, zone)}-to-{Day(report.ToUtc.AddTicks(-1), zone)}.csv";
    }

    /// <summary>A moment as its day on the workspace's calendar, yyyy-MM-dd.</summary>
    private static string Day(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static byte[] Write(ClubReportDto report, DateTime nowUtc)
    {
        var csv = new CsvExport(Headers);
        // Days on the workspace's calendar, as the periods are: its midnight is the evening before in UTC.
        var zone = RegionalSettings.ZoneOf(report.TimeZone);
        string Day(DateTime utc) => ClubReportCsv.Day(utc, zone);
        var asOf = Day(report.AsOfUtc.AddTicks(-1));
        var span = $"{Day(report.FromUtc)} to {Day(report.ToUtc.AddTicks(-1))}";

        void Flow(string inName, string outName, string netName, IReadOnlyList<ClubMoneyFlowDto> flows)
        {
            foreach (var flow in flows)
            {
                for (var i = 0; i < report.Periods.Count && i < flow.ByPeriod.Count; i++)
                {
                    var period = report.Periods[i].Start;
                    var amounts = flow.ByPeriod[i];
                    csv.Mixed(inName, period, flow.Currency, null, Money(amounts.In), null);
                    csv.Mixed(outName, period, flow.Currency, null, Money(amounts.Out), null);
                    csv.Mixed(netName, period, flow.Currency, null, Money(amounts.Net), null);
                }
                csv.Mixed($"{netName}, in all", span, flow.Currency, null, Money(flow.Net), null);
            }
        }

        Flow("Cash received", "Cash paid back", "Cash revenue", report.Cash);
        Flow("Charged", "Voided", "Accrual revenue", report.Accrual);
        foreach (var kind in report.AccrualByKind)
            csv.Mixed("Accrual revenue by kind", span, kind.Currency, kind.Kind, Money(kind.Net), null);

        foreach (var a in report.Receivables)
        {
            csv.Mixed("Receivables: current", asOf, a.Currency, "Not yet due", Money(a.Current), null);
            csv.Mixed("Receivables: 1-30 days", asOf, a.Currency, "Days past due", Money(a.Days1To30), null);
            csv.Mixed("Receivables: 31-60 days", asOf, a.Currency, "Days past due", Money(a.Days31To60), null);
            csv.Mixed("Receivables: 61-90 days", asOf, a.Currency, "Days past due", Money(a.Days61To90), null);
            csv.Mixed("Receivables: over 90 days", asOf, a.Currency, "Days past due", Money(a.Over90), null);
            csv.Mixed("Receivables: total", asOf, a.Currency, "Open charges", Money(a.Total), a.OpenCharges);
        }
        foreach (var g in report.GiftCards)
            csv.Mixed("Gift cards outstanding", asOf, g.Currency, "Cards with value left", Money(g.Outstanding), g.Cards);
        csv.Mixed("Loyalty points outstanding", asOf, report.Points.Currency,
            $"{Count(report.Points.Points)} points at {Count(report.Points.PointsPerUnit)} a unit", Money(report.Points.Value), report.Points.Members);
        foreach (var m in report.Mrr)
            csv.Mixed("Monthly recurring revenue", Day(nowUtc), m.Currency, "Running subscriptions", Money(m.Monthly), m.Subscriptions);

        var e = report.Events;
        csv.Mixed("Event fill rate", span, null, $"{Count(e.SeatsTaken)} of {Count(e.Capacity)} seats on {Count(e.LimitedEvents)} events with a limit",
            e.FillRate, e.Events);
        csv.Mixed("Event no-show rate", span, null, $"{Count(e.NoShows)} not come, {Count(e.CheckedIn)} checked in, {Count(e.NotMarked)} not marked",
            e.NoShowRate, e.Ended);
        return csv.ToBytes();
    }

    /// <summary>An amount to the cent, written with its two decimals whatever the database's sum kept.</summary>
    private static decimal Money(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero) + 0.00m;

    private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);
}
