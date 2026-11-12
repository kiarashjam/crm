namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>The club's reports (IClubReports).</summary>
    public static class ClubReports
    {
        public static readonly Error PeriodInvalid = new(
            "ClubReport.PeriodInvalid", "A report covers a period that ends after it starts, of at most three years.");

        public static readonly Error GrainInvalid = new(
            "ClubReport.GrainInvalid", "A report is by day, week or month.");

        public static readonly Error TooManyPeriods = new(
            "ClubReport.TooManyPeriods", "That is too many days to show one by one: pick weeks or months for a period this long.");
    }
}
