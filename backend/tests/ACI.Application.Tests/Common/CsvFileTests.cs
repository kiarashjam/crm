using System.Text;
using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Reading a spreadsheet saved as text, the way the importer does.
/// </summary>
/// <remarks>
/// The browser's reader split on line breaks and then on commas. A Swiss Excel file
/// (";" between columns) came through as one column, a notes cell with a line break
/// in it became two rows, and a doubled quote stayed doubled.
/// </remarks>
public class CsvFileTests
{
    private static CsvFile Read(string text, int maxRows = 1000) => ReadBytes(Encoding.UTF8.GetBytes(text), maxRows);

    private static CsvFile ReadBytes(byte[] bytes, int maxRows = 1000)
    {
        var result = CsvFile.Read(bytes, maxRows);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
        return result.Value;
    }

    [Fact]
    public void ReadsTheSemicolonsSwissExcelWrites()
    {
        var file = Read("Name;E-Mail;Firma\r\nJean Dupont;jean@example.ch;Dupont, Fils & Cie\r\n");

        file.Separator.Should().Be(';');
        file.SeparatorName.Should().Be("semicolon");
        file.Headers.Should().Equal("Name", "E-Mail", "Firma");
        file.Rows.Should().ContainSingle();
        file.Rows[0].Cells.Should().Equal("Jean Dupont", "jean@example.ch", "Dupont, Fils & Cie");
    }

    [Fact]
    public void ReadsCommasAndTabs()
    {
        Read("name,email\nJean,jean@example.ch\n").Separator.Should().Be(',');
        Read("name\temail\nJean\tjean@example.ch\n").Separator.Should().Be('\t');
    }

    [Fact]
    public void AFileCanSayWhichSeparatorItUses()
    {
        // Excel writes "sep=;" for exactly this, and does not show it as a row.
        var file = Read("sep=,\r\nname;nickname,email\r\nJean;JD,jean@example.ch\r\n");

        file.Separator.Should().Be(',');
        file.Headers.Should().Equal("name;nickname", "email");
        file.Rows[0].Number.Should().Be(2, "the header is row 1, as in the spreadsheet");
    }

    [Fact]
    public void AQuotedCellKeepsItsLineBreaksSeparatorsAndQuotes()
    {
        var file = Read("name,notes,email\r\n"
            + "Jean,\"Met at the \"\"Summer Gala\"\"\r\nWants a call, after 5pm\",jean@example.ch\r\n"
            + "Anne,,anne@example.ch\r\n");

        file.Rows.Should().HaveCount(2, "a line break inside quotes is part of the cell, not a new row");
        file.Rows[0].Cells.Should().Equal("Jean", "Met at the \"Summer Gala\"\r\nWants a call, after 5pm", "jean@example.ch");
        file.Rows[1].Cells.Should().Equal("Anne", "", "anne@example.ch");
        file.Rows[1].Number.Should().Be(3, "a multi-line cell is still one spreadsheet row");
    }

    [Fact]
    public void RowNumbersCountBlankRowsAsTheSpreadsheetDoes()
    {
        var file = Read("name,email\n\nJean,jean@example.ch\n,\nAnne,anne@example.ch");

        file.Rows.Select(r => r.Number).Should().Equal(3, 5);
    }

    [Fact]
    public void AQuoteInsideAnUnquotedCellIsJustText()
    {
        var file = Read("name,screen\nJean,5\" wide\n");

        file.Rows[0].Cell(1).Should().Be("5\" wide");
    }

    [Fact]
    public void CellsAreTrimmedAndShortRowsReadAsEmpty()
    {
        var file = Read("name,email,phone\n  Jean  , jean@example.ch \n");

        file.Rows[0].Cell(0).Should().Be("Jean");
        file.Rows[0].Cell(1).Should().Be("jean@example.ch");
        file.Rows[0].Cell(2).Should().Be("", "the row has no third cell");
    }

    [Fact]
    public void BlankHeadersAreNamedAfterTheirPlace()
    {
        Read("name,,email\nJean,x,jean@example.ch\n").Headers.Should().Equal("name", "Column 2", "email");
    }

    [Fact]
    public void ReadsUtf8WithOrWithoutItsByteOrderMark()
    {
        var text = "Name;Ort\nJürg;Zürich\n";
        Read(text).Rows[0].Cell(1).Should().Be("Zürich");

        var withBom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        var file = ReadBytes(withBom);
        file.Headers[0].Should().Be("Name", "the byte-order mark is not part of the first header");
        file.Encoding.Should().Be("UTF-8");
    }

    [Fact]
    public void ReadsTheWindowsCodePageExcelSavesCsvIn()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1252).GetBytes("Name;Ort;Betrag\nJürg;Zürich;€ 50\n");

        var file = ReadBytes(bytes);

        file.Encoding.Should().Be("Windows-1252");
        file.Rows[0].Cells.Should().Equal("Jürg", "Zürich", "€ 50");
    }

    [Fact]
    public void ReadsTheUnicodeTextExcelSaves()
    {
        // "Unicode Text" from Excel: UTF-16 with a byte-order mark, tabs between columns.
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Name\tOrt\r\nJürg\tZürich\r\n")).ToArray();

        var file = ReadBytes(bytes);

        file.Encoding.Should().Be("UTF-16");
        file.Separator.Should().Be('\t');
        file.Rows[0].Cells.Should().Equal("Jürg", "Zürich");
    }

    [Fact]
    public void ASemicolonInsideQuotedCellsDoesNotMakeACommaFileLookLikeASemicolonOne()
    {
        var file = Read("name,notes,email\n"
            + "Jean,\"a; b; c\",jean@example.ch\n"
            + "Anne,\"d; e\",anne@example.ch\n");

        file.Separator.Should().Be(',');
        file.Rows[0].Cell(1).Should().Be("a; b; c");
    }

    [Fact]
    public void AQuoteThatNeverClosesIsReportedWithItsRow()
    {
        var result = CsvFile.Read(Encoding.UTF8.GetBytes("name,notes\nJean,ok\nAnne,\"never closed\nBob,fine\n"), 1000);

        result.IsFailure.Should().BeTrue("reading on would put the rest of the file in one cell");
        result.Error.Code.Should().Be("Import.UnclosedQuote");
        result.Error.Description.Should().Contain("Row 3");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n \r\n")]
    public void AnEmptyFileIsRefused(string text)
    {
        var result = CsvFile.Read(Encoding.UTF8.GetBytes(text), 1000);

        result.Error.Code.Should().Be("Import.Empty");
    }

    [Fact]
    public void AHeaderWithNothingUnderItIsRefused()
    {
        CsvFile.Read(Encoding.UTF8.GetBytes("name,email\n\n"), 1000).Error.Code.Should().Be("Import.NoRows");
    }

    [Fact]
    public void MoreRowsThanAllowedAreRefusedRatherThanCutShort()
    {
        var text = "name,email\n" + string.Concat(Enumerable.Range(1, 4).Select(i => $"P{i},p{i}@example.ch\n"));

        CsvFile.Read(Encoding.UTF8.GetBytes(text), 3).Error.Code.Should().Be("Import.TooManyRows");
        CsvFile.Read(Encoding.UTF8.GetBytes(text), 4).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AOneColumnFileIsReadAsOneColumn()
    {
        var file = Read("email\njean@example.ch\nanne@example.ch\n");

        file.Headers.Should().Equal("email");
        file.Rows.Should().HaveCount(2);
    }
}
