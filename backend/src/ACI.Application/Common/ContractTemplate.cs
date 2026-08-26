using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// The result of filling a contract template.
/// </summary>
/// <param name="Body">The text, with every known field substituted.</param>
/// <param name="UnresolvedFields">
/// Fields the template asked for that we had no value for, in the order they
/// appear, without duplicates.
/// </param>
public record ContractMergeResult(string Body, IReadOnlyList<string> UnresolvedFields);

/// <summary>
/// Filling a contract template with what the CRM knows.
/// </summary>
/// <remarks>
/// <para>
/// The one thing this must not do is silently drop a field it cannot fill. A
/// template that says <c>Dear {{lead.name}},</c> against a lead with no name
/// would otherwise produce "Dear ," — and the CRM user, who is reading a draft
/// they expect to be complete, is exactly the person least likely to notice.
/// So an unfillable field is LEFT IN PLACE, visibly, and also reported, so the
/// UI can refuse to send until it is dealt with.
/// </para>
/// <para>
/// Deliberately not a general template engine. No conditionals, no loops, no
/// includes — a contract is a document, and every construct added here is a way
/// for the text somebody signs to differ from the text somebody reviewed.
/// </para>
/// </remarks>
public static class ContractTemplate
{
    /// <summary>
    /// Matches <c>{{ field.name }}</c> with optional inner whitespace. Field names
    /// are restricted to word characters and dots so a stray brace in prose
    /// cannot turn into a placeholder.
    /// </summary>
    private static readonly Regex FieldPattern = new(
        @"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Substitutes every field present in <paramref name="values"/>.
    /// </summary>
    /// <remarks>
    /// Substituted values are trimmed of spaces and tabs but NOT newlines, so an
    /// optional clause can supply its own line break. See the note at the
    /// substitution itself.
    ///
    /// A value that is present but blank counts as UNRESOLVED. "We have an empty
    /// string for the counterparty's address" and "we know their address is
    /// nothing" are not the same claim, and only one of them belongs in a
    /// contract.
    /// </remarks>
    public static ContractMergeResult Fill(string template, IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return new ContractMergeResult(string.Empty, Array.Empty<string>());
        }

        var unresolved = new List<string>();

        var body = FieldPattern.Replace(template, match =>
        {
            var field = match.Groups[1].Value;
            if (values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                // HORIZONTAL whitespace only. A full Trim() also strips newlines,
                // which broke the one pattern that needs them: because there are
                // deliberately no conditionals here, an optional line is expressed
                // as a value that carries its own leading break and label —
                // "\nPhone:     +41 …". Trimming that produced
                // "Email: a@b.chPhone: +41 …" in a real contract.
                return value.Trim(' ', '\t');
            }
            if (!unresolved.Contains(field, StringComparer.Ordinal))
            {
                unresolved.Add(field);
            }
            // Left in place on purpose — see the class remarks.
            return match.Value;
        });

        return new ContractMergeResult(body, unresolved);
    }

    /// <summary>Every field a template refers to, in order, without duplicates.</summary>
    public static IReadOnlyList<string> FieldsUsed(string template)
    {
        if (string.IsNullOrEmpty(template)) return Array.Empty<string>();
        var seen = new List<string>();
        foreach (Match m in FieldPattern.Matches(template))
        {
            var field = m.Groups[1].Value;
            if (!seen.Contains(field, StringComparer.Ordinal)) seen.Add(field);
        }
        return seen;
    }

    /// <summary>
    /// The starting template a new organisation gets.
    /// </summary>
    /// <remarks>
    /// Written as PLAIN TEXT, not Markdown, and that is deliberate. The signing
    /// page and the executed email both show the exact characters that were
    /// signed — a renderer is a layer that can silently change emphasis, swallow a
    /// stray bracket, or read a clause number as a list, and what the counterparty
    /// reads has to be what the hash covers. An earlier draft of this template used
    /// Markdown headings, which meant the executed copy someone keeps as their
    /// record arrived reading "**Between:**". The fix is a template that needs no
    /// renderer, not a renderer.
    ///
    /// A SKELETON, not legal advice and not anybody's actual terms. The bracketed
    /// clauses are there to be replaced by whatever this organisation really
    /// agrees with its members; nothing here has been reviewed by a lawyer and the
    /// UI says so. It exists so the flow can be exercised end to end on day one
    /// rather than starting from an empty box.
    /// </remarks>
    public const string DefaultTemplate = """
        MEMBERSHIP AGREEMENT
        ====================

        Between:   {{org.name}} ("the Club")
        And:       {{lead.name}} ("the Member")
        Date:      {{today}}


        1. MEMBERSHIP
        -------------
        The Club grants the Member access to its facilities and services on the
        terms set out in this agreement, beginning on {{contract.startDate}}.


        2. FEES
        -------
        The Member agrees to pay {{contract.fee}}, payable {{contract.paymentTerms}}.


        3. TERM AND RENEWAL
        -------------------
        This agreement runs for {{contract.term}} from the start date and renews on
        the terms described in the Club's rules unless either party gives notice as
        set out in clause 4.


        4. NOTICE AND CANCELLATION
        --------------------------
        Either party may end this agreement by giving {{contract.noticePeriod}}
        written notice. Fees already paid are treated as described in the Club's
        rules.


        5. CLUB RULES
        -------------
        The Member agrees to observe the Club's rules as published from time to
        time. The Club will give reasonable notice of any material change.


        6. CONTACT DETAILS
        ------------------
        Member:    {{lead.name}}
        Email:     {{lead.email}}{{lead.phoneClause}}


        7. GOVERNING LAW
        ----------------
        This agreement is governed by the laws of {{contract.jurisdiction}}.


        ------------------------------------------------------------------
        By signing below, both parties agree to be bound by this agreement.
        """;
}
