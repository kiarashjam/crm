using System.Text.RegularExpressions;

namespace ACI.Application.Common.Pdf;

/// <summary>What a run of contract text is, typographically.</summary>
public enum ContractBlockKind
{
    /// <summary>The document's own top-level heading.</summary>
    Title,
    /// <summary>A numbered clause heading.</summary>
    Clause,
    /// <summary>A "Label:   value" row, set as two aligned columns.</summary>
    Definition,
    /// <summary>A horizontal rule the author drew.</summary>
    Rule,
    /// <summary>Running prose, to be re-wrapped to the measure.</summary>
    Paragraph,
}

/// <summary>
/// One typographic block of a contract.
/// </summary>
/// <param name="Kind">How to set it.</param>
/// <param name="Text">The words. For a definition row, the value.</param>
/// <param name="Label">A definition row's label, without its colon.</param>
public readonly record struct ContractBlock(ContractBlockKind Kind, string Text, string? Label = null);

/// <summary>
/// Reads the structure a contract's plain text already has.
/// </summary>
/// <remarks>
/// <para>
/// The contract template is deliberately plain text, not Markdown, because the
/// signing page and the emailed copy must show the exact characters that the
/// document hash covers. But plain text written by a person is not shapeless: it
/// underlines its headings, aligns its labels, and rules off its sections. This
/// reads that shape so the PDF can SET it, while the plain text stays the
/// instrument.
/// </para>
/// <para>
/// It is a typesetter, not a parser of meaning. It never changes a word, a number,
/// or a mark of punctuation. What it does discard is pure decoration that the
/// typesetting replaces — the row of equals signs under a heading becomes the
/// heading's weight and size, and the line of dashes becomes a rule. And it
/// re-wraps prose to the measure, which is what setting text means.
/// </para>
/// </remarks>
public static class ContractBlocks
{
    /// <summary>A line of nothing but one repeated ruling character.</summary>
    private static readonly Regex RuleLine = new(@"^\s*(=+|-+|_+|\*+)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// A label and its value, separated by two or more spaces.
    /// </summary>
    /// <remarks>
    /// Two spaces, not one, is the whole distinction. "Email:     jean@example.ch"
    /// is a field in a table; "Note: the fee is payable monthly" is a sentence that
    /// happens to contain a colon, and setting it as a table row would be wrong. The
    /// label is also capped in length and forbidden from containing a colon, so a
    /// long sentence with a mid-clause colon cannot be mistaken for one.
    /// </remarks>
    private static readonly Regex DefinitionRow = new(
        @"^(?<label>[A-Za-z][^:\r\n]{0,23}):[ \t]{2,}(?<value>.*)$", RegexOptions.Compiled);

    /// <summary>The shortest run of ruling characters that counts as an underline.</summary>
    private const int MinRuleLength = 3;

    /// <summary>Splits a contract body into blocks.</summary>
    public static IReadOnlyList<ContractBlock> Parse(string? body)
    {
        var blocks = new List<ContractBlock>();
        if (string.IsNullOrWhiteSpace(body)) return blocks;

        var lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            blocks.Add(new ContractBlock(ContractBlockKind.Paragraph, string.Join(' ', paragraph)));
            paragraph.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            // A ruling line standing on its own — nothing above it to underline — is
            // a rule the author drew, not a heading marker.
            if (IsRuleLine(trimmed))
            {
                FlushParagraph();
                blocks.Add(new ContractBlock(ContractBlockKind.Rule, ""));
                continue;
            }

            // Underlined? Then THIS line is the heading and the next is its marker.
            var underline = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            if (IsRuleLine(underline))
            {
                FlushParagraph();
                var kind = underline[0] == '=' ? ContractBlockKind.Title : ContractBlockKind.Clause;
                blocks.Add(new ContractBlock(kind, trimmed));
                i++; // the underline itself is now set as weight, not printed
                continue;
            }

            var definition = DefinitionRow.Match(line);
            if (definition.Success)
            {
                FlushParagraph();
                blocks.Add(new ContractBlock(
                    ContractBlockKind.Definition,
                    definition.Groups["value"].Value.Trim(),
                    definition.Groups["label"].Value.Trim()));
                continue;
            }

            paragraph.Add(trimmed);
        }

        FlushParagraph();
        return blocks;
    }

    private static bool IsRuleLine(string trimmed)
        => trimmed.Length >= MinRuleLength && RuleLine.IsMatch(trimmed);

    /// <summary>
    /// Splits a clause heading into its number and its words, or null if it has no number.
    /// </summary>
    /// <remarks>
    /// Only so the number can be set in the accent colour. Both halves are drawn, in
    /// order, with the separator intact — nothing is dropped by recognising it.
    /// </remarks>
    public static (string Number, string Words)? SplitClauseNumber(string heading)
    {
        var m = Regex.Match(heading, @"^(?<num>\d+(\.\d+)*\.?)\s+(?<rest>\S.*)$");
        return m.Success ? (m.Groups["num"].Value, m.Groups["rest"].Value) : null;
    }
}
