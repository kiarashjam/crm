using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Common.Pdf;
using FluentAssertions;

namespace ACI.Application.Tests.Common;

/// <summary>
/// A deal's value and line items as a contract's merge fields: the fee table the member signs,
/// in the deal's currency, with the VAT inside each rate's lines.
/// </summary>
/// <remarks>
/// Amounts here stay under a thousand, or are read without their group separator: de-CH writes
/// one thousand with a typographic apostrophe on one ICU version and a straight one on another.
/// </remarks>
public class ContractDealFieldsTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly ContractFeeLine[] Lines =
    [
        new("Gold membership, annual", 1, 480m, 8.1m),
        new("Locker", 2, 60m, 8.1m),
        new("Wine tasting", 3, 25.5m, 2.6m),
    ];

    [Fact]
    public void TheFeeTableListsEachLineWithItsQuantityPriceVatAndTotal_ThenTheTotalAndTheVatInside()
    {
        var table = ContractDealFields.FeeTable(Lines, "CHF", Invariant);
        var rows = table.Split('\n');

        rows[0].Should().Contain("Item").And.Contain("Qty").And.Contain("Unit price (CHF)").And.Contain("VAT").And.Contain("Total (CHF)");
        rows.Should().Contain(r => r.StartsWith("Gold membership, annual") && r.Contains("| 8.1% |") && r.EndsWith("480.00"));
        rows.Should().Contain(r => r.StartsWith("Locker") && r.Contains(" 2 |") && r.Contains("60.00") && r.EndsWith("120.00"));
        rows.Should().Contain(r => r.StartsWith("Wine tasting") && r.Contains("| 2.6% |") && r.EndsWith("76.50"));
        // 480 + 120 + 76.50; the VAT is each rate's share of its lines (600 × 8.1 / 108.1, 76.50 × 2.6 / 102.6).
        rows.Should().Contain(r => r.StartsWith("Total ") && r.EndsWith("676.50"));
        rows.Should().Contain(r => r.StartsWith("VAT 8.1% included") && r.EndsWith("44.96"));
        rows.Should().Contain(r => r.StartsWith("VAT 2.6% included") && r.EndsWith("1.94"));
        table.Should().EndWith("Prices include VAT at the rates shown.");
    }

    [Fact]
    public void ItsColumnsLineUp_SoItReadsAsATableOnTheSigningPage()
    {
        var rows = ContractDealFields.FeeTable(Lines, "CHF", Invariant).Split('\n').TakeWhile(r => r.Length > 0).ToList();
        var bars = rows.Where(r => r.Contains('|')).Select(r => Enumerable.Range(0, r.Length).Where(i => r[i] == '|').ToList()).ToList();
        bars.Should().AllSatisfy(positions => positions.Should().Equal(bars[0], "every row's bars sit in the same columns"));
        var rules = rows.Where(r => r.Contains('+')).ToList();
        rules.Should().HaveCount(2, "one rule under the header and one above the totals");
        rules.Should().AllSatisfy(rule => Enumerable.Range(0, rule.Length).Where(i => rule[i] == '+').Should().Equal(bars[0]));
    }

    [Fact]
    public void LinesWithNoVatRate_LeaveTheVatColumnOutAltogether()
    {
        var table = ContractDealFields.FeeTable([new("Consulting", 2, 150m, null)], "EUR", Invariant);
        table.Should().NotContain("VAT").And.Contain("Unit price (EUR)").And.Contain("300.00");
    }

    [Fact]
    public void ANameWithABarOrALineBreak_StaysInItsOwnCell()
    {
        var table = ContractDealFields.FeeTable([new("Gold | founding\nmember", 1, 10m, null), new("Locker", 1, 5m, null)], "CHF", Invariant);
        var blocks = ContractBlocks.Parse(table);
        var rows = blocks.Single(b => b.Kind == ContractBlockKind.Table).Rows!;
        rows.Should().AllSatisfy(r => r.Should().HaveCount(4, "item, quantity, unit price and total: no VAT column without a rate"));
        rows.Should().Contain(r => r[0] == "Gold / founding member");
    }

    [Fact]
    public void TheMergeFieldsCarryTheDealsFiguresInItsCurrency()
    {
        var fields = ContractDealFields.For("Gold for the Muster family", 676.5m, "chf", Lines, Invariant);
        fields["deal.name"].Should().Be("Gold for the Muster family");
        fields["deal.currency"].Should().Be("CHF");
        fields["deal.value"].Should().Be("CHF 676.50");
        fields["deal.total"].Should().Be("CHF 676.50");
        fields["deal.vatIncluded"].Should().Be("CHF 44.96 (8.1%), CHF 1.94 (2.6%)");
        fields["deal.lineItems"].Should().Contain("Gold membership, annual");
    }

    [Fact]
    public void ADealWithoutLines_HasItsValueAsTheTotal_AndNoFeeTableToFill()
    {
        var fields = ContractDealFields.For("Corporate", 950m, "CHF", [], Invariant);
        fields["deal.total"].Should().Be("CHF 950.00");
        fields.Should().NotContainKey("deal.lineItems", "an empty fee table would read as a contract for nothing; the field stays unfilled");
        ContractDealFields.For("No money yet", null, "CHF", [], Invariant).Should().NotContainKey("deal.value");
    }

    [Fact]
    public void TheDealTemplateFilledFromADeal_LeavesOnlyWhatTheDealCannotKnow()
    {
        var values = ContractDealFields.For("Gold", 676.5m, "CHF", Lines, Invariant);
        values["org.name"] = "Pavillon 46";
        values["lead.name"] = "Anna Muster";
        values["lead.email"] = "anna@example.ch";
        values["lead.phoneClause"] = "";
        values["today"] = "13 November 2026";
        values["contract.depositClause"] = ContractTemplate.DepositClause("CHF 500.00");

        var merged = ContractTemplate.Fill(ContractTemplate.DealTemplate, values, ContractTemplate.OptionalFields);

        merged.UnresolvedFields.Should().BeEquivalentTo(
            ["contract.startDate", "contract.paymentTerms", "contract.term", "contract.noticePeriod", "contract.jurisdiction"]);
        merged.Body.Should().Contain("The Member agrees to pay CHF 676.50")
            .And.Contain("\nA deposit of CHF 500.00 is payable on signing.");
    }

    [Fact]
    public void ADepositClauseIsNothingWithoutADeposit()
    {
        ContractTemplate.DepositClause(null).Should().BeEmpty();
        ContractTemplate.OptionalFields.Should().Contain("contract.depositClause");
    }
}
