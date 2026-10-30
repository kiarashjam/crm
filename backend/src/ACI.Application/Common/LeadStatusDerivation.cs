using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// Works out a lead's status from its five-phase pipeline. A C# port of
/// <c>src/app/pages/leads/leadStatusSync.ts</c>, so the server can keep the status
/// true when it is the one moving the pipeline: an e-signed contract, a merge, an
/// API call.
/// </summary>
/// <remarks>
/// <para>
/// The browser used to be the only place that knew the rule, so every server-side
/// writer left the status where it was. A counterparty who signed at night left a
/// lead reading "Contract Pending" next to a pipeline that said "Signed", and the
/// filters, stats, score and reports all followed the stale status.
/// </para>
/// <para>
/// Everything here is pure and mirrors the TypeScript one-for-one: the same stage
/// names, synonyms, ladder and holds. The two are kept in step by tests on both
/// sides that hash the outcome of every reachable pipeline (see
/// <c>leadStatusSync.parity.json</c>). Change one, and the other side's test fails
/// until it matches.
/// </para>
/// <para>
/// The invariants carried over from the browser: dates never decide the stage;
/// only exact literals count as progress (legacy or hand-edited JSON holding
/// "YES" or 1 reads as unset); a status is only ever taken from the
/// organisation's own list; a parked or unrecognised status is never written over.
/// </para>
/// </remarks>
public static class LeadStatusDerivation
{
    /* ---------------------------------------------------------- stages */

    /// <summary>Vocabulary-independent names for where a lead has got to. Same strings as the TypeScript.</summary>
    public static class Stage
    {
        public const string New = "new";
        public const string Attempted = "attempted";
        public const string Contacted = "contacted";
        public const string MeetingScheduled = "meeting_scheduled";
        public const string MeetingHeld = "meeting_held";
        public const string Qualified = "qualified";
        public const string ContractPending = "contract_pending";
        public const string ContractSent = "contract_sent";
        public const string Signed = "signed";
        public const string DepositPaid = "deposit_paid";
        public const string Unqualified = "unqualified";
        public const string Lost = "lost";
    }

    /// <summary>
    /// Position on the progress ladder. The two terminals sit off it (null), which is
    /// what makes "moving to a terminal is never a regression" fall out of a comparison.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int?> StageTier = new Dictionary<string, int?>
    {
        [Stage.New] = 0,
        [Stage.Attempted] = 1,
        [Stage.Contacted] = 2,
        [Stage.MeetingScheduled] = 3,
        [Stage.MeetingHeld] = 4,
        [Stage.Qualified] = 5,
        [Stage.ContractPending] = 6,
        [Stage.ContractSent] = 7,
        [Stage.Signed] = 8,
        [Stage.DepositPaid] = 9,
        [Stage.Unqualified] = null,
        [Stage.Lost] = null,
    };

    /* ---------------------------------------------------------- the pipeline */

    /// <summary>
    /// The categorical fields of a pipeline: the only ones that decide a stage. Values
    /// are kept exactly as stored, because anything but the exact literal reads as unset.
    /// </summary>
    public sealed record Pipeline
    {
        public string? OutreachStatus { get; init; }
        public string? ContactOutcome { get; init; }
        public bool? MeetingAttended { get; init; }
        public bool? StillInterested { get; init; }
        public string? ContractStatus { get; init; }
        public string? ContractSigned { get; init; }
        public string? DepositStatus { get; init; }

        public static readonly Pipeline Empty = new();
    }

    /// <summary>
    /// Reads a lead's PipelineState JSON the way <c>parsePipeline</c> does: anything
    /// that is not a JSON object is an empty pipeline, a field of the wrong type is
    /// unset, and the legacy <c>depositPaid</c> boolean becomes <c>depositStatus</c>.
    /// </summary>
    /// <remarks>
    /// Legacy <c>false</c> reads as "pending", not "never paid": it was written when
    /// there was no way to say they never paid, and reading it as a failure now would
    /// mark existing leads lost after the fact.
    /// </remarks>
    public static Pipeline ParsePipeline(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Pipeline.Empty;
        JsonDocument doc;
        try
        {
            // Through UTF-8 with replacement: a JavaScript string can hold half of a
            // surrogate pair (an emoji cut short), which JSON.parse reads without
            // complaint and parsing the string directly refuses outright.
            doc = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));
        }
        catch (JsonException)
        {
            return Pipeline.Empty;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return Pipeline.Empty;

            // The last of a repeated key wins, as it does in JSON.parse. A name or value
            // that escapes half of a surrogate pair ("\ud83d") is valid JSON that .NET
            // will not turn into a string. It can never equal a field name or a value
            // the rule compares against, so it is skipped, or read as unset, rather
            // than failing the whole save the way it used to.
            var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                string name;
                try { name = property.Name; }
                catch (InvalidOperationException) { continue; }
                fields[name] = property.Value;
            }

            string? Text(string name)
            {
                if (!fields.TryGetValue(name, out var v) || v.ValueKind != JsonValueKind.String) return null;
                try { return v.GetString(); }
                catch (InvalidOperationException) { return null; }
            }
            bool? Flag(string name) =>
                fields.TryGetValue(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? v.GetBoolean()
                    : null;

            var deposit = Text("depositStatus");
            // Only when depositStatus is absent altogether: a present null is "defined"
            // in JavaScript, and the browser does not fold over it either.
            if (!fields.ContainsKey("depositStatus") && Flag("depositPaid") is bool paid)
                deposit = paid ? "paid" : "pending";

            return new Pipeline
            {
                OutreachStatus = Text("outreachStatus"),
                ContactOutcome = Text("contactOutcome"),
                MeetingAttended = Flag("meetingAttended"),
                StillInterested = Flag("stillInterested"),
                ContractStatus = Text("contractStatus"),
                ContractSigned = Text("contractSigned"),
                DepositStatus = deposit,
            };
        }
    }

    /* ---------------------------------------------------------- deriving a stage */

    /// <summary>The single stage that best describes a pipeline, and why.</summary>
    /// <param name="Phase">Pipeline phase (1-5) that produced it; 0 when nothing is recorded.</param>
    /// <param name="Terminal">Lost or unqualified: forward progress has stopped.</param>
    /// <param name="Because">The signal that actually decided it, in words.</param>
    /// <param name="Rule">Stable id for the machine-readable trailer.</param>
    /// <param name="Conflicts">Recorded negatives a later positive outcome overrode, so none is silently dropped.</param>
    public sealed record DerivedStage(
        string Stage, int Phase, bool Terminal, string Because, string Rule, IReadOnlyList<string> Conflicts);

    private static List<string> NegativeSignals(Pipeline p)
    {
        var list = new List<string>();
        if (p.ContactOutcome == "not_interested") list.Add("Not interested at outreach");
        if (p.MeetingAttended == false) list.Add("No-show at meeting");
        if (p.StillInterested == false) list.Add("Not interested after meeting");
        if (p.ContractStatus == "profile_rejected") list.Add("Profile rejected");
        if (p.ContractStatus == "no_longer_interested") list.Add("No longer interested");
        if (p.ContractSigned == "no") list.Add("Contract declined");
        if (p.DepositStatus == "not_paid") list.Add("Deposit never paid");
        return list;
    }

    /// <summary>
    /// Reduces a pipeline to one stage, scanning the deepest phase first: the newest
    /// recorded fact wins, so a banked deposit outranks a stale "not interested" and a
    /// declined signature outranks "still interested".
    /// </summary>
    public static DerivedStage DeriveStage(Pipeline p)
    {
        var (stage, phase, terminal, because, rule) = DeriveCore(p);
        // Only a positive outcome can override a negative; a terminal is the negative.
        IReadOnlyList<string> conflicts = terminal ? Array.Empty<string>() : NegativeSignals(p);
        return new DerivedStage(stage, phase, terminal, because, rule, conflicts);
    }

    private static (string, int, bool, string, string) DeriveCore(Pipeline p)
    {
        // Phase 5: deposit. "pending" is the default, not a setback, so it decides nothing.
        if (p.DepositStatus == "paid") return (Stage.DepositPaid, 5, false, "Deposit paid", "phase5_deposit_paid");
        if (p.DepositStatus == "not_paid") return (Stage.Lost, 5, true, "Deposit never paid", "phase5_deposit_never_paid");

        // Phase 4: signature.
        if (p.ContractSigned == "yes") return (Stage.Signed, 4, false, "Contract signed", "phase4_signed");
        if (p.ContractSigned == "no") return (Stage.Lost, 4, true, "Contract declined", "phase4_declined");
        // Awaiting a signature is real phase-4 progress but not a new fact about their
        // intent, so it ranks here (keeping the derivation monotone) yet gives way to
        // any recorded negative: an old contract still in the inbox of someone who said
        // they are out must not read as a live opportunity.
        if (p.ContractSigned == "pending" && NegativeSignals(p).Count == 0)
            return (Stage.ContractSent, 4, false, "Awaiting signature", "phase4_pending");

        // Phase 3: contract.
        if (p.ContractStatus == "yes") return (Stage.ContractSent, 3, false, "Contract sent", "phase3_contract_sent");
        if (p.ContractStatus == "to_be_sent") return (Stage.ContractPending, 3, false, "Contract to be sent", "phase3_contract_to_send");
        // "We rejected them" is a different fact from "they walked away".
        if (p.ContractStatus == "profile_rejected") return (Stage.Unqualified, 3, true, "Profile rejected", "phase3_profile_rejected");
        if (p.ContractStatus == "no_longer_interested") return (Stage.Lost, 3, true, "No longer interested", "phase3_no_longer_interested");

        // Phase 2: after the meeting. Interest is recorded after attendance, so it is
        // the more recent word and is checked first.
        if (p.StillInterested == false) return (Stage.Lost, 2, true, "Not interested after meeting", "phase2_not_interested");
        if (p.StillInterested == true) return (Stage.Qualified, 2, false, "Still interested after the meeting", "phase2_qualified");
        if (p.MeetingAttended == true) return (Stage.MeetingHeld, 2, false, "Attended the meeting", "phase2_meeting_held");
        // A missed meeting with no word on intent usually needs rescheduling, not
        // writing off: every phase has an explicit "they are out" for that.
        if (p.MeetingAttended == false) return (Stage.Contacted, 2, false, "Missed the meeting", "phase2_no_show");

        // Phase 1: outreach. A follow-up is still only "we have spoken" and must not
        // outrank a booked meeting.
        if (p.ContactOutcome == "not_interested") return (Stage.Lost, 1, true, "Not interested", "phase1_not_interested");
        if (p.ContactOutcome == "meeting_scheduled") return (Stage.MeetingScheduled, 1, false, "Meeting scheduled", "phase1_meeting_scheduled");
        if (p.ContactOutcome == "follow_up") return (Stage.Contacted, 1, false, "Follow-up to be scheduled", "phase1_follow_up");
        if (p.OutreachStatus == "contacted") return (Stage.Contacted, 1, false, "Contacted", "phase1_contacted");
        if (p.OutreachStatus == "attempted_no_answer") return (Stage.Attempted, 1, false, "Attempted contact — no answer", "phase1_attempted");

        return (Stage.New, 0, false, "No pipeline activity recorded", "no_signal");
    }

    /* ---------------------------------------------------------- the org's vocabulary */

    /// <summary>One of the organisation's own statuses.</summary>
    public sealed record StatusOption(string Id, string Name, int? DisplayOrder = null);

    /// <summary>How a status was found for a stage.</summary>
    public static class StatusMatch
    {
        public const string Exact = "exact";
        public const string Synonym = "synonym";
        public const string Fallback = "fallback";
    }

    /// <summary>A status name taken verbatim from the org's list, and the stage it really represents after any downgrade.</summary>
    /// <param name="Id">The option's id, only when it is a real GUID.</param>
    public sealed record ResolvedStatus(string Name, string? Id, string Match, string Canonical);

    /// <summary>
    /// Synonyms per stage, most specific first, so an org with both "Negotiation" and
    /// "Contract Signed" gets the precise one. Bare "open" is deliberately absent:
    /// every rep reads it as "untouched", so it must never be a forward move's target.
    /// "contract pending" is listed under both qualified and contract_pending because
    /// one status covers both in the agreed vocabulary; a shared label resolves to the
    /// same option, so crossing between the two stages leaves the status unchanged.
    /// The generic top-of-ladder labels belong to signed alone, or paying a deposit
    /// could move a "Closed Won" lead back to "Open Deal".
    /// </summary>
    private static readonly (string Stage, string[] Synonyms)[] StageSynonyms =
    {
        (Stage.New, new[] { "new", "new lead" }),
        (Stage.Attempted, new[] { "attempted contact", "attempted", "no answer", "left message", "tried" }),
        (Stage.Contacted, new[] { "contacted", "reached out", "in contact", "follow up", "follow-up", "nurturing" }),
        (Stage.MeetingScheduled, new[] { "meeting scheduled", "meeting booked", "demo scheduled", "appointment set" }),
        // "connected" means we actually got in front of them: attendance earns it, not a booking.
        (Stage.MeetingHeld, new[] { "meeting held", "meeting done", "demo done", "discovery done", "connected", "met", "in progress" }),
        (Stage.Qualified, new[] { "qualified", "sql", "mql", "interested", "contract pending" }),
        (Stage.ContractPending, new[] { "contract to send", "contract to be sent", "contract pending", "drafting contract", "preparing proposal", "proposal in progress" }),
        (Stage.ContractSent, new[] { "awaiting signature", "contract sent", "proposal sent", "quote sent", "contract out", "negotiation", "proposal" }),
        (Stage.Signed, new[] { "contract signed", "signed", "closed won", "won", "open deal" }),
        (Stage.DepositPaid, new[] { "deposit paid", "deposit received", "deposit", "customer" }),
        // Both terminals accept the combined label, or an org whose only negative is
        // "Lost / Not Interested" would leave a rejected profile's status untouched.
        (Stage.Unqualified, new[] { "unqualified", "disqualified", "not a fit", "rejected", "lost / not interested", "lost or not interested" }),
        (Stage.Lost, new[] { "lost", "closed lost", "dead", "not interested", "no longer interested", "lost / not interested", "lost or not interested" }),
    };

    /// <summary>
    /// Statuses meaning "a person deliberately parked this lead". Never written over.
    /// Not the only defence: an unrecognised status is protected as well, because a
    /// list like this is never complete.
    /// </summary>
    private static readonly string[] StickyStatusLabels =
    {
        "do not contact", "dnc", "unsubscribed", "opted out", "junk", "spam",
        "on hold", "hold", "nurture", "recycled", "bad timing", "archived",
    };

    /// <summary>Walked when an org has no status for a stage. Never upward: claiming more progress than the list can say would over-promise.</summary>
    private static readonly string[] DowngradeChain =
    {
        Stage.DepositPaid, Stage.Signed, Stage.ContractSent, Stage.ContractPending,
        Stage.Qualified, Stage.MeetingHeld, Stage.MeetingScheduled, Stage.Contacted, Stage.Attempted, Stage.New,
    };

    /// <summary>
    /// What JavaScript's trim() and \s count as white space. .NET's own Trim() and \s
    /// differ in two characters: they count U+0085 (next line) and not U+FEFF (the
    /// zero-width no-break space a pasted label can carry). With .NET's set a status
    /// like that was read differently here than in the browser, so the preview on
    /// the tracker promised a move the server then did not make, or the reverse.
    /// </summary>
    private static readonly char[] JsWhiteSpace =
    {
        '\t', '\n', '\v', '\f', '\r', ' ', (char)0x00A0, (char)0x1680,
        (char)0x2000, (char)0x2001, (char)0x2002, (char)0x2003, (char)0x2004, (char)0x2005,
        (char)0x2006, (char)0x2007, (char)0x2008, (char)0x2009, (char)0x200A,
        (char)0x2028, (char)0x2029, (char)0x202F, (char)0x205F, (char)0x3000, (char)0xFEFF,
    };

    private static readonly Regex Separators = new("[_-]+", RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new("[" + Regex.Escape(new string(JsWhiteSpace)) + "]+", RegexOptions.CultureInvariant);
    private static readonly Regex GuidPattern = new(
        "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Case, spacing and separators do not matter when matching a label.</summary>
    private static string Norm(string? s) =>
        Spaces.Replace(Separators.Replace((s ?? "").Trim(JsWhiteSpace).ToLowerInvariant(), " "), " ");

    private static string[] SynonymsOf(string stage) =>
        StageSynonyms.FirstOrDefault(s => s.Stage == stage).Synonyms ?? Array.Empty<string>();

    private static StatusOption? FindOption(IReadOnlyList<StatusOption> options, string label)
    {
        var target = Norm(label);
        return options.FirstOrDefault(o => Norm(o.Name) == target);
    }

    private static string? IdOf(StatusOption option) => GuidPattern.IsMatch(option.Id) ? option.Id : null;

    private static ResolvedStatus? ResolveExactStage(string stage, IReadOnlyList<StatusOption> options)
    {
        var synonyms = SynonymsOf(stage);
        for (var i = 0; i < synonyms.Length; i++)
        {
            var hit = FindOption(options, synonyms[i]);
            if (hit != null) return new ResolvedStatus(hit.Name, IdOf(hit), i == 0 ? StatusMatch.Exact : StatusMatch.Synonym, stage);
        }
        return null;
    }

    /// <summary>
    /// A real status for a stage from the org's own list: its synonyms first, then
    /// down the ladder. Terminals never degrade. Null when nothing fits, and the
    /// caller then leaves the status alone rather than guess.
    /// </summary>
    /// <param name="overrides">Stage → status-name pins, the escape hatch for a fully renamed vocabulary.</param>
    public static ResolvedStatus? ResolveStatus(
        string stage, IReadOnlyList<StatusOption> options, IReadOnlyDictionary<string, string>? overrides = null)
    {
        if (options.Count == 0) return null;

        if (overrides != null && overrides.TryGetValue(stage, out var pinned) && !string.IsNullOrEmpty(pinned))
        {
            var hit = FindOption(options, pinned);
            if (hit != null) return new ResolvedStatus(hit.Name, IdOf(hit), StatusMatch.Exact, stage);
        }

        var direct = ResolveExactStage(stage, options);
        if (direct != null) return direct;

        if (!StageTier.TryGetValue(stage, out var tier) || tier == null) return null;

        var start = Array.IndexOf(DowngradeChain, stage);
        if (start == -1) return null;
        for (var i = start + 1; i < DowngradeChain.Length; i++)
        {
            var weaker = DowngradeChain[i];
            // Never degrade to New. Every other step down understates progress, which
            // is tolerable; New asserts nothing has happened to a lead that has been
            // worked. An org whose list predates the agreed stages used to have its
            // first outreach write "New" over "New", which reads as the feature not
            // working at all. Resolving to nothing instead leaves visible drift.
            if (weaker == Stage.New) break;
            // Pins count on the way down too: a renamed vocabulary pins several stages.
            if (overrides != null && overrides.TryGetValue(weaker, out var pinnedWeaker) && !string.IsNullOrEmpty(pinnedWeaker))
            {
                var pinnedHit = FindOption(options, pinnedWeaker);
                if (pinnedHit != null) return new ResolvedStatus(pinnedHit.Name, IdOf(pinnedHit), StatusMatch.Fallback, weaker);
            }
            var hit = ResolveExactStage(weaker, options);
            if (hit != null) return hit with { Match = StatusMatch.Fallback, Canonical = weaker };
        }
        return null;
    }

    /// <summary>The stage a status label means, or null for a label we do not recognise (a custom or parking status). Exact synonyms only.</summary>
    public static string? ClassifyStatusLabel(string? label)
    {
        var target = Norm(label);
        if (target.Length == 0) return null;
        foreach (var (stage, synonyms) in StageSynonyms)
        {
            if (synonyms.Any(s => Norm(s) == target)) return stage;
        }
        return null;
    }

    public static bool IsStickyStatus(string? label)
    {
        var target = Norm(label);
        return StickyStatusLabels.Any(s => Norm(s) == target);
    }

    /* ---------------------------------------------------------- planning a write */

    /// <summary>Why a derived status was not written. Same codes as the TypeScript.</summary>
    public static class SkipReason
    {
        public const string StatusesLoading = "statuses_loading";
        public const string StatusesUnavailable = "statuses_unavailable";
        public const string Converted = "converted";
        public const string Disabled = "disabled";
        public const string NoSignal = "no_signal";
        public const string Unresolvable = "unresolvable";
        public const string Noop = "noop";
    }

    /// <summary>Why a derived status was only offered, not written.</summary>
    public static class SuggestReason
    {
        public const string StickyStatus = "sticky_status";
        public const string UnrecognisedStatus = "unrecognised_status";
    }

    public static class PlanKind
    {
        public const string Apply = "apply";
        public const string Suggest = "suggest";
        public const string Skip = "skip";
    }

    /// <summary>What should happen to a lead's status given its pipeline.</summary>
    /// <param name="Kind">apply, suggest or skip.</param>
    /// <param name="From">The status the lead had.</param>
    /// <param name="To">The status to write (apply) or offer (suggest).</param>
    /// <param name="Reason">The skip reason, or the first suggest reason.</param>
    /// <param name="Reasons">Every suggest reason that applies.</param>
    public sealed record Plan(
        string Kind, DerivedStage Derived, string From = "", ResolvedStatus? To = null,
        string? Reason = null, IReadOnlyList<string>? Reasons = null)
    {
        public bool Applies => Kind == PlanKind.Apply && To != null;
    }

    /// <summary>The inputs <c>planStatusSync</c> takes, with the same meaning.</summary>
    public sealed record Context
    {
        public required Pipeline Pipeline { get; init; }
        public string? CurrentStatus { get; init; }
        public required IReadOnlyList<StatusOption> StatusOptions { get; init; }
        /// <summary>The browser holds writes until the org's own list arrives; the server always has it, or knows it is empty.</summary>
        public bool StatusesLoaded { get; init; }
        public bool StatusesUnavailable { get; init; }
        public bool IsConverted { get; init; }
        /// <summary>The browser's old per-user opt-out. Kept so the port is one-for-one; the server never turns it off.</summary>
        public bool? Enabled { get; init; }
        public IReadOnlyDictionary<string, string>? Overrides { get; init; }
    }

    /// <summary>
    /// Decides what, if anything, should happen to the status. Every recorded step
    /// moves it, in either direction, because nobody types a status any more and a
    /// corrected step has to be able to move it back. The only holds left are not
    /// about progress: a parked status (do not contact, on hold) and a status we do
    /// not recognise are offered the change, never given it.
    /// </summary>
    public static Plan PlanStatusSync(Context ctx)
    {
        var derived = DeriveStage(ctx.Pipeline);

        if (!ctx.StatusesLoaded)
            return new Plan(PlanKind.Skip, derived, Reason: ctx.StatusesUnavailable ? SkipReason.StatusesUnavailable : SkipReason.StatusesLoading);
        if (ctx.IsConverted) return new Plan(PlanKind.Skip, derived, Reason: SkipReason.Converted);
        if (ctx.Enabled == false) return new Plan(PlanKind.Skip, derived, Reason: SkipReason.Disabled);
        // Nothing recorded. Clearing a pipeline must not drag the status back to New
        // behind anyone's back.
        if (derived.Phase == 0 && derived.Stage == Stage.New) return new Plan(PlanKind.Skip, derived, Reason: SkipReason.NoSignal);

        var to = ResolveStatus(derived.Stage, ctx.StatusOptions, ctx.Overrides);
        if (to == null) return new Plan(PlanKind.Skip, derived, Reason: SkipReason.Unresolvable);

        var from = ctx.CurrentStatus ?? "";
        if (Norm(to.Name) == Norm(from)) return new Plan(PlanKind.Skip, derived, Reason: SkipReason.Noop);

        var fromStage = from.Length > 0 ? ClassifyStatusLabel(from) : Stage.New;
        var reasons = new List<string>();
        if (IsStickyStatus(from)) reasons.Add(SuggestReason.StickyStatus);
        if (from.Length > 0 && fromStage == null) reasons.Add(SuggestReason.UnrecognisedStatus);
        if (reasons.Count > 0) return new Plan(PlanKind.Suggest, derived, from, to, reasons[0], reasons);

        return new Plan(PlanKind.Apply, derived, from, to);
    }

    /// <summary>
    /// The timeline entry for an automatic move, so "why is this lead Signed?" has an
    /// answer on the lead itself, ending in a trailer support can read without a repro.
    /// </summary>
    public static (string Subject, string Body) DescribeChange(string from, ResolvedStatus to, DerivedStage derived)
    {
        var lines = new List<string>
        {
            $"Because the pipeline records: {derived.Because}{(derived.Phase > 0 ? $" (Phase {derived.Phase})" : "")}.",
            from.Length > 0 ? $"Status moved from \"{from}\" to \"{to.Name}\"." : $"Status set to \"{to.Name}\".",
        };
        if (to.Match == StatusMatch.Fallback)
            lines.Add($"No status matched \"{derived.Stage}\", so the closest earlier stage was used.");
        if (derived.Conflicts.Count > 0)
            lines.Add($"Overridden: {string.Join("; ", derived.Conflicts)} — a later positive outcome takes precedence.");
        lines.Add($"[auto-status rule={derived.Rule} canonical={to.Canonical} match={to.Match} phase={derived.Phase}]");
        return ($"Status auto-updated to {to.Name}", string.Join("\n", lines));
    }
}
