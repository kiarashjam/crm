using System.Text;
using ACI.Application.Common;
using FluentAssertions;
using Xunit;

namespace ACI.Application.Tests.Common;

/// <summary>
/// CSV files split by a comma or by a semicolon: a cell is quoted when it holds the file's own
/// separator, and formulas stay text either way.
/// </summary>
public class CsvExportTests
{
    private static string[] Lines(CsvExport csv) =>
        Encoding.UTF8.GetString(csv.ToBytes()).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void ACommaFileQuotesCommas_AndLeavesSemicolonsAlone()
    {
        var csv = new CsvExport("Name", "Deal value").Row("Hotel Bellevue, Zermatt", "CHF 40000; EUR 25000");
        Lines(csv).Should().Equal("Name,Deal value", "\"Hotel Bellevue, Zermatt\",CHF 40000; EUR 25000");
    }

    [Fact]
    public void ASemicolonFileQuotesSemicolons_AndLeavesCommasAlone()
    {
        var csv = new CsvExport(CsvExport.Semicolon, "Name", "Deal value", "Amount")
            .Row("Hotel Bellevue, Zermatt", "CHF 40000; EUR 25000")
            .Mixed("=SUM(A1)", "Müller \"der Ältere\"", -12.5m);
        Lines(csv).Should().Equal(
            "Name;Deal value;Amount",
            "Hotel Bellevue, Zermatt;\"CHF 40000; EUR 25000\"",
            "'=SUM(A1);\"Müller \"\"der Ältere\"\"\";-12.5");
    }

    [Fact]
    public void TheWorkspacesChoiceNamesTheSeparator_AndAnythingElseIsAComma()
    {
        CsvExport.SeparatorFor(true).Should().Be(';');
        CsvExport.SeparatorFor(false).Should().Be(',');
        Lines(new CsvExport('\t', "a", "b")).Should().Equal(new[] { "a,b" }, "only a comma or a semicolon is ever written");
    }

    [Fact]
    public void EitherFileStartsWithTheByteOrderMarkExcelNeedsForUtf8()
    {
        new CsvExport(CsvExport.Semicolon, "Prénom").ToBytes().Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
    }
}
