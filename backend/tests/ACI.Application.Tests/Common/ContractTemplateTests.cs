using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Filling a contract template.
/// </summary>
/// <remarks>
/// Almost all of this is about the field we CANNOT fill. A template engine that
/// drops unknown placeholders produces "Dear ," in a document somebody is about to
/// sign, and the reviewer — who expects a finished draft — is the least likely
/// person to spot it.
/// </remarks>
public class ContractTemplateTests
{
    private static Dictionary<string, string?> Values(params (string Key, string? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    [Fact]
    public void SubstitutesWhatItKnows()
    {
        var result = ContractTemplate.Fill(
            "Between {{org.name}} and {{lead.name}}.",
            Values(("org.name", "Pavillon 46"), ("lead.name", "Jean Dupont")));

        result.Body.Should().Be("Between Pavillon 46 and Jean Dupont.");
        result.UnresolvedFields.Should().BeEmpty();
    }

    [Fact]
    public void ToleratesWhitespaceInsideTheBraces()
    {
        var result = ContractTemplate.Fill("{{ lead.name }}", Values(("lead.name", "Léa")));
        result.Body.Should().Be("Léa");
    }

    [Fact]
    public void TrimsTheSubstitutedValue()
    {
        // A trailing space out of a database column would otherwise land in the
        // middle of a sentence.
        var result = ContractTemplate.Fill("Dear {{lead.name}},", Values(("lead.name", "  Jean  ")));
        result.Body.Should().Be("Dear Jean,");
    }

    [Fact]
    public void LeavesAnUnfillableFieldVISIBLE_AndReportsIt()
    {
        // The whole point. Dropping it would produce "Dear ," and look finished.
        var result = ContractTemplate.Fill("Dear {{lead.name}},", Values());

        result.Body.Should().Be("Dear {{lead.name}},");
        result.UnresolvedFields.Should().Equal("lead.name");
    }

    [Fact]
    public void TreatsAPresentButBlankValueAsUnresolved()
    {
        // "We hold an empty string for their address" and "their address is
        // nothing" are different claims, and only one belongs in a contract.
        foreach (var blank in new string?[] { null, "", "   ", "\t" })
        {
            var result = ContractTemplate.Fill("Address: {{lead.address}}", Values(("lead.address", blank)));
            // `Equal(params)` takes every argument as an expected element, so the
            // reason goes on a separate assertion rather than as a second arg.
            result.UnresolvedFields.Should().ContainSingle(because: $"blank={blank ?? "null"}")
                .Which.Should().Be("lead.address");
            result.Body.Should().Contain("{{lead.address}}");
        }
    }

    [Fact]
    public void KeepsANewlineInsideAMergeValue()
    {
        // There are deliberately no conditionals, so an OPTIONAL line is expressed
        // as a value carrying its own leading break and label. A full Trim() ate
        // that break and produced "Email: a@b.chPhone: +41 …" in a real contract.
        var result = ContractTemplate.Fill(
            "Email:     {{lead.email}}{{lead.phoneClause}}",
            Values(("lead.email", "  a@b.ch  "), ("lead.phoneClause", "\nPhone:     +41 22 555 0134")));

        result.Body.Should().Be("Email:     a@b.ch\nPhone:     +41 22 555 0134");
        result.UnresolvedFields.Should().BeEmpty();
    }

    [Fact]
    public void AnAbsentOptionalClauseLeavesNoTrace()
    {
        // The other half of the same pattern: when there is no phone, the line
        // must vanish rather than leave a dangling label. An absent value is
        // unresolved, so sending is blocked until the caller supplies "" — which
        // it does by passing the empty clause explicitly.
        var result = ContractTemplate.Fill(
            "Email:     {{lead.email}}{{lead.phoneClause}}\n\nNext",
            Values(("lead.email", "a@b.ch"), ("lead.phoneClause", " ")));

        // A blank clause is reported rather than silently dropped, so the UI asks.
        result.UnresolvedFields.Should().Contain("lead.phoneClause");
    }

    [Fact]
    public void ReportsEachMissingFieldOnceInOrder()
    {
        var result = ContractTemplate.Fill(
            "{{b}} {{a}} {{b}} {{c}} {{a}}",
            Values(("c", "known")));

        result.UnresolvedFields.Should().Equal("b", "a");
    }

    [Fact]
    public void DoesNotTurnStrayBracesInProseIntoPlaceholders()
    {
        // Contract prose contains braces and mathematics. Only the strict
        // {{word.word}} form is a field.
        const string prose = "The set {a, b} applies. Also {{ not a field! }} and {{}}.";
        var result = ContractTemplate.Fill(prose, Values());

        result.Body.Should().Be(prose);
        result.UnresolvedFields.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotRecursivelyExpandASubstitutedValue()
    {
        // A value containing a placeholder must stay literal, or a lead named
        // "{{org.name}}" could rewrite clauses of the contract.
        var result = ContractTemplate.Fill(
            "Party: {{lead.name}}",
            Values(("lead.name", "{{org.name}}"), ("org.name", "Pavillon 46")));

        result.Body.Should().Be("Party: {{org.name}}");
    }

    [Fact]
    public void HandlesAnEmptyTemplate()
    {
        var result = ContractTemplate.Fill("", Values(("a", "b")));
        result.Body.Should().BeEmpty();
        result.UnresolvedFields.Should().BeEmpty();
    }

    [Fact]
    public void FieldsUsedListsEveryFieldOnceInOrder()
    {
        ContractTemplate.FieldsUsed("{{b}} {{a}} {{b}}").Should().Equal("b", "a");
        ContractTemplate.FieldsUsed("").Should().BeEmpty();
        ContractTemplate.FieldsUsed("no fields here").Should().BeEmpty();
    }

    [Fact]
    public void TheDefaultTemplateOnlyUsesFieldsWeCanActuallySupply()
    {
        // Guards against the seeded skeleton shipping a placeholder that nothing
        // ever fills, which would make every new organisation's first draft
        // permanently unsendable.
        var supported = new[]
        {
            "org.name", "lead.name", "lead.email", "lead.phoneClause", "today",
            "contract.startDate", "contract.fee", "contract.paymentTerms",
            "contract.term", "contract.noticePeriod", "contract.jurisdiction",
        };

        ContractTemplate.FieldsUsed(ContractTemplate.DefaultTemplate)
            .Should().OnlyContain(f => supported.Contains(f));
    }

    [Fact]
    public void TheDefaultTemplateFillsCompletelyWhenEverythingIsKnown()
    {
        var values = ContractTemplate.FieldsUsed(ContractTemplate.DefaultTemplate)
            .ToDictionary(f => f, f => (string?)$"[{f}]", StringComparer.Ordinal);

        var result = ContractTemplate.Fill(ContractTemplate.DefaultTemplate, values);

        result.UnresolvedFields.Should().BeEmpty();
        result.Body.Should().NotContain("{{");
    }
}
