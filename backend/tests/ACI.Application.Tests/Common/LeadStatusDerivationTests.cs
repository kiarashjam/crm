using ACI.Application.Common;
using static ACI.Application.Common.LeadStatusDerivation;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The rules for deriving a lead's status from its pipeline, ported case for case
/// from src/app/pages/leads/leadStatusSync.test.ts: same inputs, same answer.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is a product decision, so these are statements about behaviour ("a
/// mailed contract does not look like a won deal"), kept in the order of the
/// TypeScript file so the two can be read side by side. LeadStatusParityTests
/// checks every reachable pipeline on top of this.
/// </para>
/// <para>
/// Not ported, because the server does not do them: statusDrift and
/// previewStatusChange (the browser's views of a lead), lostReason (the tracker
/// banner) and the status groups used for counting.
/// </para>
/// </remarks>
public class LeadStatusDerivationTests
{
    private static readonly string[] Fallback =
    {
        "New", "Open", "Attempted Contact", "Contacted", "Connected",
        "In Progress", "Qualified", "Unqualified", "Open Deal", "Lost",
    };

    private static List<StatusOption> Opts(params string[] names) => names.Select(n => new StatusOption(n, n)).ToList();

    private static readonly List<StatusOption> DefaultOpts = Opts(Fallback);

    /// <summary>The pipeline as the JSON a lead holds.</summary>
    private static Pipeline P(string json) => ParsePipeline(json);

    private static DerivedStage D(string json) => DeriveStage(P(json));

    private static Plan PlanFor(
        string pipeline, string current = "New", IReadOnlyList<StatusOption>? options = null,
        bool loaded = true, bool unavailable = false, bool converted = false, bool? enabled = true,
        IReadOnlyDictionary<string, string>? overrides = null) =>
        PlanStatusSync(new Context
        {
            Pipeline = P(pipeline),
            CurrentStatus = current,
            StatusOptions = options ?? DefaultOpts,
            StatusesLoaded = loaded,
            StatusesUnavailable = unavailable,
            IsConverted = converted,
            Enabled = enabled,
            Overrides = overrides,
        });

    /* ------------------------------------------ deriveStage: phase 1 outreach */

    [Fact]
    public void NoPipelineAtAllYieldsNoSignal()
    {
        var d = D("{}");
        d.Stage.Should().Be(Stage.New);
        d.Phase.Should().Be(0);
    }

    [Fact]
    public void AttemptedNoAnswer() => D("""{"outreachStatus":"attempted_no_answer"}""").Stage.Should().Be(Stage.Attempted);

    [Fact]
    public void Contacted() => D("""{"outreachStatus":"contacted"}""").Stage.Should().Be(Stage.Contacted);

    [Fact]
    public void ABookedMeetingOutranksABareContacted() =>
        D("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled"}""").Stage.Should().Be(Stage.MeetingScheduled);

    [Fact]
    public void AFollowUpIsStillOnlyContactedAndDoesNotOutrankABookedMeeting()
    {
        var followUp = D("""{"outreachStatus":"contacted","contactOutcome":"follow_up"}""");
        var booked = D("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled"}""");
        followUp.Stage.Should().Be(Stage.Contacted);
        StageTier[followUp.Stage]!.Value.Should().BeLessThan(StageTier[booked.Stage]!.Value);
    }

    [Fact]
    public void NotInterestedIsAnExplicitTerminal()
    {
        var d = D("""{"outreachStatus":"contacted","contactOutcome":"not_interested"}""");
        d.Stage.Should().Be(Stage.Lost);
        d.Terminal.Should().BeTrue();
    }

    /* ------------------------------------------- deriveStage: phase 2 meeting */

    [Fact]
    public void Attended() => D("""{"meetingAttended":true}""").Stage.Should().Be(Stage.MeetingHeld);

    [Fact]
    public void StillInterestedAfterTheMeetingIsQualified() =>
        D("""{"meetingAttended":true,"stillInterested":true}""").Stage.Should().Be(Stage.Qualified);

    [Fact]
    public void NotInterestedAfterTheMeetingIsLost()
    {
        var d = D("""{"meetingAttended":true,"stillInterested":false}""");
        d.Stage.Should().Be(Stage.Lost);
        d.Terminal.Should().BeTrue();
    }

    [Fact]
    public void ABareNoShowIsRealProgressNotAWriteOff()
    {
        // Silence about intent means the lead is still open: a missed meeting usually
        // needs rescheduling, and every phase has an explicit way to say "they are out".
        var d = D("""{"meetingAttended":false}""");
        d.Stage.Should().Be(Stage.Contacted);
        d.Terminal.Should().BeFalse();
        d.Because.Should().MatchRegex("(?i)missed the meeting");
    }

    [Fact]
    public void ANoShowWhoIsStillInterestedIsQualifiedNotLost() =>
        D("""{"meetingAttended":false,"stillInterested":true}""").Stage.Should().Be(Stage.Qualified);

    /* ------------------------------------------ deriveStage: phase 3 contract */

    [Fact]
    public void ContractSent() => D("""{"contractStatus":"yes"}""").Stage.Should().Be(Stage.ContractSent);

    [Fact]
    public void AContractStillToBeSentHasItsOwnStage() =>
        D("""{"contractStatus":"to_be_sent"}""").Stage.Should().Be(Stage.ContractPending);

    [Fact]
    public void WeRejectedThemIsUnqualifiedNotLost()
    {
        var d = D("""{"contractStatus":"profile_rejected"}""");
        d.Stage.Should().Be(Stage.Unqualified);
        d.Terminal.Should().BeTrue();
    }

    [Fact]
    public void TheyWalkedAwayIsLost() => D("""{"contractStatus":"no_longer_interested"}""").Stage.Should().Be(Stage.Lost);

    /* ---------------------------------- deriveStage: phase 4/5 signature, deposit */

    [Fact]
    public void Signed() => D("""{"contractSigned":"yes"}""").Stage.Should().Be(Stage.Signed);

    [Fact]
    public void AwaitingSignatureReadsAsContractOutNeverAsAWonDeal() =>
        D("""{"contractSigned":"pending"}""").Stage.Should().Be(Stage.ContractSent);

    [Fact]
    public void ADeclinedSignatureIsLost() => D("""{"contractSigned":"no"}""").Stage.Should().Be(Stage.Lost);

    [Fact]
    public void DepositPaid() => D("""{"depositStatus":"paid"}""").Stage.Should().Be(Stage.DepositPaid);

    [Fact]
    public void ADepositNotPaidYetIsNotASetback() =>
        D("""{"contractSigned":"yes","depositStatus":"pending"}""").Stage.Should().Be(Stage.Signed);

    [Fact]
    public void TheLegacyDepositBooleanStillCounts()
    {
        // parsePipeline folds depositPaid into depositStatus; false is "not yet", not "never".
        D("""{"depositPaid":true}""").Stage.Should().Be(Stage.DepositPaid);
        D("""{"contractSigned":"yes","depositPaid":false}""").Stage.Should().Be(Stage.Signed);
    }

    /* ------------------------------------ precedence: the deepest phase wins */

    [Fact]
    public void ABankedDepositOutranksAStalePhaseOneNotInterested()
    {
        var d = D("""{"contactOutcome":"not_interested","depositStatus":"paid"}""");
        d.Stage.Should().Be(Stage.DepositPaid);
        d.Terminal.Should().BeFalse();
    }

    [Fact]
    public void ADeclinedSignatureOutranksStillInterestedFromTheMeeting() =>
        D("""{"stillInterested":true,"contractSigned":"no"}""").Stage.Should().Be(Stage.Lost);

    [Fact]
    public void AStatedNotInterestedOutranksAnUnsignedContractStillInTheirInbox() =>
        D("""{"stillInterested":false,"contractSigned":"pending"}""").Stage.Should().Be(Stage.Lost);

    [Fact]
    public void APendingSignatureStillRegistersWhenNothingElseIsKnown() =>
        D("""{"contractSigned":"pending"}""").Stage.Should().Be(Stage.ContractSent);

    [Fact]
    public void APendingSignatureAgreesWithASentContract() =>
        D("""{"contractStatus":"yes","contractSigned":"pending"}""").Stage.Should().Be(Stage.ContractSent);

    /* ------------------------------------- invariant: dates never move the status */

    private static readonly string[] CategoricalStates =
    {
        "{}",
        """{"outreachStatus":"attempted_no_answer"}""",
        """{"outreachStatus":"contacted"}""",
        """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled"}""",
        """{"outreachStatus":"contacted","contactOutcome":"follow_up"}""",
        """{"outreachStatus":"contacted","contactOutcome":"not_interested"}""",
        """{"meetingAttended":true}""",
        """{"meetingAttended":false}""",
        """{"meetingAttended":true,"stillInterested":true}""",
        """{"meetingAttended":true,"stillInterested":false}""",
        """{"contractStatus":"yes"}""",
        """{"contractStatus":"to_be_sent"}""",
        """{"contractStatus":"profile_rejected"}""",
        """{"contractStatus":"no_longer_interested"}""",
        """{"contractSigned":"yes"}""",
        """{"contractSigned":"pending"}""",
        """{"contractSigned":"no"}""",
        """{"depositStatus":"paid"}""",
        """{"depositStatus":"pending"}""",
    };

    [Fact]
    public void AddingOrClearingAnyDateLeavesTheStageIdentical()
    {
        var dateFields = new[] { "outreachDate", "meetingDate", "contractSentDate", "signatureDate", "paymentDate" };
        foreach (var json in CategoricalStates)
        {
            var expected = D(json).Stage;
            var body = json.TrimStart('{').TrimEnd('}');
            string With(string extra) => "{" + (body.Length > 0 ? body + "," : "") + extra + "}";
            foreach (var field in dateFields)
            {
                D(With($"\"{field}\":\"2026-05-13\"")).Stage.Should().Be(expected, $"{field} added to {json}");
                D(With($"\"{field}\":\"\"")).Stage.Should().Be(expected, $"{field} cleared on {json}");
            }
            D(With(string.Join(",", dateFields.Select(f => $"\"{f}\":\"2026-05-13\"")))).Stage.Should().Be(expected);
        }
    }

    /* ---------------------------- invariant: recording progress never moves back */

    [Fact]
    public void APositiveEditNeverLowersTheDerivedStage()
    {
        // Exhaustive over every combination of the categorical fields. In the browser
        // this caught a real bug: with a pending signature evaluated as a last resort,
        // recording the earlier outreach dropped a lead from contract_sent to contacted.
        string?[] outreach = { null, "attempted_no_answer", "contacted" };
        string?[] outcome = { null, "meeting_scheduled", "follow_up", "not_interested" };
        bool?[] flags = { null, true, false };
        string?[] cstatus = { null, "yes", "to_be_sent", "profile_rejected", "no_longer_interested" };
        string?[] csigned = { null, "yes", "pending", "no" };
        string?[] deposit = { null, "paid", "pending", "not_paid" };

        var positiveEdits = new (string Field, Func<Pipeline, Pipeline> Apply)[]
        {
            ("outreachStatus", p => p with { OutreachStatus = "attempted_no_answer" }),
            ("outreachStatus", p => p with { OutreachStatus = "contacted" }),
            ("contactOutcome", p => p with { ContactOutcome = "follow_up" }),
            ("contactOutcome", p => p with { ContactOutcome = "meeting_scheduled" }),
            ("meetingAttended", p => p with { MeetingAttended = true }),
            ("stillInterested", p => p with { StillInterested = true }),
            ("contractStatus", p => p with { ContractStatus = "to_be_sent" }),
            ("contractStatus", p => p with { ContractStatus = "yes" }),
            ("contractSigned", p => p with { ContractSigned = "pending" }),
            ("contractSigned", p => p with { ContractSigned = "yes" }),
            ("depositStatus", p => p with { DepositStatus = "paid" }),
        };
        static bool IsSet(Pipeline p, string field) => field switch
        {
            "outreachStatus" => p.OutreachStatus != null,
            "contactOutcome" => p.ContactOutcome != null,
            "meetingAttended" => p.MeetingAttended != null,
            "stillInterested" => p.StillInterested != null,
            "contractStatus" => p.ContractStatus != null,
            "contractSigned" => p.ContractSigned != null,
            _ => p.DepositStatus != null,
        };

        var regressions = new List<string>();
        foreach (var o in outreach) foreach (var c in outcome) foreach (var ma in flags) foreach (var si in flags)
        foreach (var cs in cstatus) foreach (var sg in csigned) foreach (var dp in deposit)
        {
            var basePipeline = new Pipeline
            {
                OutreachStatus = o, ContactOutcome = c, MeetingAttended = ma, StillInterested = si,
                ContractStatus = cs, ContractSigned = sg, DepositStatus = dp,
            };
            var before = DeriveStage(basePipeline);
            var tierBefore = StageTier[before.Stage];
            if (tierBefore == null) continue; // terminal: off the ladder
            foreach (var (field, apply) in positiveEdits)
            {
                if (IsSet(basePipeline, field)) continue; // only genuinely new information
                var after = DeriveStage(apply(basePipeline));
                var tierAfter = StageTier[after.Stage];
                if (tierAfter == null) continue;
                if (tierAfter < tierBefore) regressions.Add($"{field}: {before.Stage} -> {after.Stage} from {basePipeline}");
            }
        }
        regressions.Take(5).Should().BeEmpty();
    }

    /* ------------------------------------- invariant: hostile JSON reads as unset */

    [Theory]
    [InlineData("""{"depositPaid":"yes"}""")]
    [InlineData("""{"depositPaid":1}""")]
    [InlineData("""{"meetingAttended":1}""")]
    [InlineData("""{"meetingAttended":"true"}""")]
    [InlineData("""{"stillInterested":"false"}""")]
    [InlineData("""{"contractSigned":"YES"}""")]
    [InlineData("""{"contractSigned":"Yes"}""")]
    [InlineData("""{"contractStatus":"Yes"}""")]
    [InlineData("""{"outreachStatus":"Contacted"}""")]
    [InlineData("""{"contactOutcome":"Meeting Scheduled"}""")]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("")]
    public void MalformedPipelineJsonReadsAsUnsetNeverAsProgress(string json)
    {
        var d = D(json);
        d.Stage.Should().Be(Stage.New);
        d.Phase.Should().Be(0);
    }

    /* ---------------------------------------- resolveStatus: default vocabulary */

    [Fact]
    public void MapsEachStageOntoTheShippedFallbackListSensibly()
    {
        string? Got(string stage) => ResolveStatus(stage, DefaultOpts)?.Name;
        Got(Stage.Attempted).Should().Be("Attempted Contact");
        Got(Stage.Contacted).Should().Be("Contacted");
        // "Connected" belongs to meeting HELD. The old list has no booked-meeting
        // label, so a booked meeting degrades one rung rather than over-claim.
        Got(Stage.MeetingScheduled).Should().Be("Contacted");
        Got(Stage.MeetingHeld).Should().Be("Connected");
        Got(Stage.Qualified).Should().Be("Qualified");
        Got(Stage.Signed).Should().Be("Open Deal");
        Got(Stage.DepositPaid).Should().Be("Open Deal");
        Got(Stage.Unqualified).Should().Be("Unqualified");
        Got(Stage.Lost).Should().Be("Lost");
    }

    [Fact]
    public void TheContractStagesDegradeToQualifiedWhenTheOrgHasNoContractStatus()
    {
        var sent = ResolveStatus(Stage.ContractSent, DefaultOpts);
        sent!.Name.Should().Be("Qualified");
        sent.Match.Should().Be(StatusMatch.Fallback);
        sent.Canonical.Should().Be(Stage.Qualified);
    }

    [Fact]
    public void NeverResolvesAForwardMoveOntoBareOpen()
    {
        foreach (var stage in new[] { Stage.Attempted, Stage.Contacted, Stage.MeetingScheduled, Stage.MeetingHeld,
                     Stage.Qualified, Stage.ContractPending, Stage.ContractSent, Stage.Signed, Stage.DepositPaid })
            ResolveStatus(stage, DefaultOpts)?.Name.Should().NotBe("Open", stage);
    }

    /* ------------------------ resolveStatus: vocabularies that predate the stages */

    [Fact]
    public void TheLegacyListResolvesEveryStageSoAnEstablishedOrgStillSyncs()
    {
        var legacy = Opts(Fallback);
        ResolveStatus(Stage.Attempted, legacy)!.Name.Should().Be("Attempted Contact");
        ResolveStatus(Stage.Attempted, legacy)!.Match.Should().Be(StatusMatch.Exact);
        ResolveStatus(Stage.Contacted, legacy)!.Name.Should().Be("Contacted");
        ResolveStatus(Stage.Qualified, legacy)!.Name.Should().Be("Qualified");
        ResolveStatus(Stage.Signed, legacy)!.Name.Should().Be("Open Deal");
        ResolveStatus(Stage.MeetingScheduled, legacy)!.Match.Should().Be(StatusMatch.Fallback);
    }

    [Fact]
    public void TheAgreedListResolvesEveryStageExactly()
    {
        var agreed = Opts("New", "Attempted Contact", "Contacted", "Connected",
            "Contract Pending", "Awaiting Signature", "Signed", "Lost / Not Interested");
        ResolveStatus(Stage.Attempted, agreed)!.Match.Should().Be(StatusMatch.Exact);
        ResolveStatus(Stage.MeetingHeld, agreed)!.Name.Should().Be("Connected");
        ResolveStatus(Stage.ContractSent, agreed)!.Name.Should().Be("Awaiting Signature");
        ResolveStatus(Stage.Signed, agreed)!.Name.Should().Be("Signed");
    }

    /* --------------------------------- resolveStatus: New is never a fallback */

    private static readonly List<StatusOption> Sparse = Opts("New", "Won", "Lost");

    [Fact]
    public void AWorkedLeadDoesNotDegradeToNew()
    {
        foreach (var stage in new[] { Stage.Attempted, Stage.Contacted, Stage.MeetingScheduled, Stage.MeetingHeld,
                     Stage.Qualified, Stage.ContractPending, Stage.ContractSent })
            ResolveStatus(stage, Sparse)?.Name.Should().NotBe("New", stage);
    }

    [Fact]
    public void ButNewItselfStillResolves()
    {
        ResolveStatus(Stage.New, Sparse)!.Name.Should().Be("New");
        ResolveStatus(Stage.New, Sparse)!.Match.Should().Be(StatusMatch.Exact);
    }

    [Fact]
    public void AnOverrideMayStillPinAStageToNewBecauseThatIsTheOrgAsking() =>
        ResolveStatus(Stage.Attempted, Sparse, new Dictionary<string, string> { [Stage.Attempted] = "New" })!.Name.Should().Be("New");

    /* ------------------------------ resolveStatus: richer and stranger vocabularies */

    [Fact]
    public void PrefersThePreciseStatusWhenAnOrgConfiguresSeveral()
    {
        var rich = Opts("New", "Contacted", "Negotiation", "Contract Signed", "Qualified", "Lost");
        ResolveStatus(Stage.Signed, rich)!.Name.Should().Be("Contract Signed");
        ResolveStatus(Stage.ContractSent, rich)!.Name.Should().Be("Negotiation");
    }

    [Fact]
    public void AnExplicitContractSentBeatsTheCoarserNegotiation() =>
        ResolveStatus(Stage.ContractSent, Opts("New", "Negotiation", "Contract Sent", "Lost"))!.Name.Should().Be("Contract Sent");

    [Fact]
    public void PayingADepositNeverMovesALeadBackwardsOutOfTheTopStatus()
    {
        var org = Opts("New", "Contacted", "Qualified", "Open Deal", "Closed Won", "Lost");
        var signed = ResolveStatus(Stage.Signed, org)!.Name;
        ResolveStatus(Stage.DepositPaid, org)!.Name.Should().Be(signed);

        var plan = PlanFor("""{"contractSigned":"yes","depositStatus":"paid"}""", signed, org);
        plan.Kind.Should().Be(PlanKind.Skip);
        plan.Reason.Should().Be(SkipReason.Noop);
    }

    [Fact]
    public void StillPrefersADepositSpecificStatusWhenTheOrgHasOne()
    {
        var org = Opts("New", "Qualified", "Closed Won", "Deposit Paid", "Lost");
        ResolveStatus(Stage.DepositPaid, org)!.Name.Should().Be("Deposit Paid");
        ResolveStatus(Stage.Signed, org)!.Name.Should().Be("Closed Won");
    }

    [Fact]
    public void DegradesDownwardOnlyNeverClaimingMoreProgressThanTheVocabularyHas()
    {
        var signed = ResolveStatus(Stage.Signed, Opts("New", "Contacted", "Lost"));
        signed!.Name.Should().Be("Contacted");
        signed.Canonical.Should().Be(Stage.Contacted);
    }

    [Fact]
    public void ReturnsNullRatherThanInventingAStatus()
    {
        ResolveStatus(Stage.Qualified, Opts("Alpha", "Beta")).Should().BeNull();
        ResolveStatus(Stage.Qualified, new List<StatusOption>()).Should().BeNull();
    }

    [Fact]
    public void TerminalsNeverDegrade()
    {
        ResolveStatus(Stage.Lost, Opts("New", "Contacted")).Should().BeNull();
        ResolveStatus(Stage.Unqualified, Opts("New", "Contacted")).Should().BeNull();
    }

    [Fact]
    public void AnOrgOverrideRescuesAFullyRenamedVocabulary()
    {
        var french = Opts("Nouveau", "Contacté", "Qualifié", "Perdu");
        ResolveStatus(Stage.Qualified, french).Should().BeNull();
        ResolveStatus(Stage.Qualified, french, new Dictionary<string, string> { [Stage.Qualified] = "Qualifié" })!.Name.Should().Be("Qualifié");
    }

    [Fact]
    public void OverridesAreHonouredOnTheDowngradePathToo()
    {
        var french = Opts("Nouveau", "Contacté", "Qualifié", "Perdu");
        var resolved = ResolveStatus(Stage.DepositPaid, french, new Dictionary<string, string> { [Stage.Qualified] = "Qualifié" });
        resolved!.Name.Should().Be("Qualifié");
        resolved.Match.Should().Be(StatusMatch.Fallback);
        resolved.Canonical.Should().Be(Stage.Qualified);
    }

    [Fact]
    public void MatchingIgnoresCaseSpacingAndSeparators()
    {
        ResolveStatus(Stage.Qualified, Opts("  QUALIFIED  "))!.Name.Should().Be("  QUALIFIED  ");
        ResolveStatus(Stage.Attempted, Opts("attempted-contact"))!.Name.Should().Be("attempted-contact");
    }

    [Fact]
    public void OnlyExposesAnIdWhenItIsARealGuid()
    {
        const string guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
        ResolveStatus(Stage.Qualified, new[] { new StatusOption(guid, "Qualified") })!.Id.Should().Be(guid);
        ResolveStatus(Stage.Qualified, new[] { new StatusOption("Qualified", "Qualified") })!.Id.Should().BeNull();
    }

    /* -------------------------------------------- classifyStatusLabel / sticky */

    [Fact]
    public void RecognisesTheShippedVocabulary()
    {
        ClassifyStatusLabel("Qualified").Should().Be(Stage.Qualified);
        ClassifyStatusLabel("Open Deal").Should().Be(Stage.Signed);
        ClassifyStatusLabel("Attempted Contact").Should().Be(Stage.Attempted);
    }

    [Fact]
    public void RefusesToGuessAtAnUnfamiliarLabel()
    {
        ClassifyStatusLabel("Cold Outreach Q3").Should().BeNull();
        ClassifyStatusLabel("Partner Referral — Pending").Should().BeNull();
        ClassifyStatusLabel("").Should().BeNull();
    }

    [Fact]
    public void FlagsDeliberateParkingStatuses()
    {
        IsStickyStatus("Do Not Contact").Should().BeTrue();
        IsStickyStatus("unsubscribed").Should().BeTrue();
        IsStickyStatus("Qualified").Should().BeFalse();
    }

    /* ------------------------------------- planStatusSync: a straightforward advance */

    [Fact]
    public void RecordingABookedMeetingMovesNewToContactedNotConnected()
    {
        var plan = PlanFor("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled"}""");
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.To!.Name.Should().Be("Contacted");
        plan.From.Should().Be("New");
    }

    [Fact]
    public void RecordingAttendanceIsWhatMovesALeadToConnected()
    {
        var plan = PlanFor("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingAttended":true}""");
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.To!.Name.Should().Be("Connected");
    }

    [Fact]
    public void NamesTheCauseAndCarriesAMachineReadableTrailer()
    {
        var plan = PlanFor("""{"meetingAttended":true,"stillInterested":true}""", "Connected");
        plan.Kind.Should().Be(PlanKind.Apply);
        var (_, body) = DescribeChange(plan.From, plan.To!, plan.Derived);
        body.Should().Contain("Still interested after the meeting");
        body.Should().MatchRegex(@"\[auto-status rule=phase2_qualified canonical=qualified match=\w+ phase=2\]");
    }

    [Fact]
    public void ASingleRecordedNegativeIsNamedAndLandsOnLost()
    {
        var plan = PlanFor("""{"contactOutcome":"not_interested"}""", "Contacted");
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.Derived.Because.Should().Be("Not interested");
        plan.To!.Name.Should().Be("Lost");
    }

    [Fact]
    public void NamesTheSignalThatActuallyDecidedTheStageNotTheShallowestOne()
    {
        var d = D("""{"contactOutcome":"not_interested","contractSigned":"no"}""");
        d.Rule.Should().Be("phase4_declined");
        d.Because.Should().Be("Contract declined");
    }

    [Fact]
    public void AnUnqualifiedStageSaysWeRejectedThem()
    {
        var d = D("""{"stillInterested":false,"contractStatus":"profile_rejected"}""");
        d.Stage.Should().Be(Stage.Unqualified);
        d.Because.Should().Be("Profile rejected");
    }

    /* ---------------------- planStatusSync: refuses to write when a human might disagree */

    [Fact]
    public void StaysSilentWhileTheOrgStatusListIsStillLoading() =>
        PlanFor("""{"depositStatus":"paid"}""", loaded: false).Reason.Should().Be(SkipReason.StatusesLoading);

    [Fact]
    public void SaysTheListIsUnavailableNotLoadingWhenItIsNeverArriving()
    {
        var plan = PlanFor("""{"depositStatus":"paid"}""", loaded: false, unavailable: true);
        plan.Kind.Should().Be(PlanKind.Skip, "naming the cause must not have turned it into a write");
        plan.Reason.Should().Be(SkipReason.StatusesUnavailable);
    }

    [Fact]
    public void OnceTheRealListArrivesTheUnavailableFlagIsIgnored() =>
        PlanFor("""{"depositStatus":"paid"}""", loaded: true, unavailable: true).Kind.Should().Be(PlanKind.Apply);

    [Fact]
    public void NeverTouchesAConvertedLead() =>
        PlanFor("""{"depositStatus":"paid"}""", converted: true).Reason.Should().Be(SkipReason.Converted);

    [Fact]
    public void HonoursTheOldPerUserOptOut() =>
        PlanFor("""{"depositStatus":"paid"}""", enabled: false).Reason.Should().Be(SkipReason.Disabled);

    [Fact]
    public void DoesNothingForAnEmptyPipelineSoClearingDoesNotDragTheStatusBack() =>
        PlanFor("{}", "Qualified").Reason.Should().Be(SkipReason.NoSignal);

    [Fact]
    public void IsANoOpWhenTheStatusAlreadyMatches() =>
        PlanFor("""{"meetingAttended":true,"stillInterested":true}""", "Qualified").Reason.Should().Be(SkipReason.Noop);

    [Fact]
    public void StaysSilentWhenTheOrgHasNoSuitableStatusAtAll() =>
        PlanFor("""{"contactOutcome":"not_interested"}""", "Alpha", Opts("Alpha", "Beta")).Reason.Should().Be(SkipReason.Unresolvable);

    [Fact]
    public void OnlySuggestsOverADeliberateParkingStatus()
    {
        var plan = PlanFor("""{"meetingAttended":true,"stillInterested":true}""", "Do Not Contact", Opts(Fallback.Append("Do Not Contact").ToArray()));
        plan.Kind.Should().Be(PlanKind.Suggest);
        plan.Reason.Should().Be(SuggestReason.StickyStatus);
    }

    [Fact]
    public void OnlySuggestsOverAnUnrecognisedCustomStatus()
    {
        var plan = PlanFor("""{"meetingAttended":true,"stillInterested":true}""", "Cold Outreach Q3", Opts(Fallback.Append("Cold Outreach Q3").ToArray()));
        plan.Kind.Should().Be(PlanKind.Suggest);
        plan.Reason.Should().Be(SuggestReason.UnrecognisedStatus);
    }

    [Fact]
    public void DoesBringAStaleLostBackInLine() =>
        PlanFor("""{"outreachStatus":"contacted"}""", "Lost").Kind.Should().Be(PlanKind.Apply);

    [Fact]
    public void StillNeverMarksALeadDeadForOneMissedMeeting()
    {
        var plan = PlanFor("""{"meetingAttended":false}""", "Connected");
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.To!.Canonical.Should().Be(Stage.Contacted);
    }

    [Fact]
    public void AutoWritesAStatedLoss() =>
        PlanFor("""{"meetingAttended":true,"stillInterested":false}""", "Connected").Kind.Should().Be(PlanKind.Apply);

    /* ----------------------------------------------- planStatusSync: either way */

    [Fact]
    public void DemotesWhenThePipelineFallsBehindTheStatusBecauseThatIsACorrection()
    {
        var plan = PlanFor("""{"outreachStatus":"contacted"}""", "Open Deal");
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.To!.Canonical.Should().Be(Stage.Contacted);
    }

    [Fact]
    public void ATerminalIsNeverARegression()
    {
        var plan = PlanFor("""{"contractSigned":"no"}""", "Open Deal");
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.To!.Name.Should().Be("Lost");
    }

    [Fact]
    public void MovingAwayFromATerminalIsAlwaysForward()
    {
        var plan = PlanFor("""{"meetingAttended":true,"stillInterested":true}""", "Lost");
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.To!.Name.Should().Be("Qualified");
    }

    /* ------------------------------------------------- conflicts are declared */

    [Fact]
    public void APositiveStageThatOverridesARecordedNegativeDeclaresTheConflict()
    {
        foreach (var json in new[]
                 {
                     """{"meetingAttended":false}""",
                     """{"meetingAttended":false,"stillInterested":true}""",
                     """{"contactOutcome":"not_interested","depositStatus":"paid"}""",
                 })
        {
            var d = D(json);
            d.Terminal.Should().BeFalse(json);
            d.Conflicts.Should().NotBeEmpty(json);
        }
    }

    [Fact]
    public void ABankedDepositOverridesAnEarlierRefusalAndSaysSo() =>
        D("""{"contactOutcome":"not_interested","depositStatus":"paid"}""").Conflicts.Should().Contain("Not interested at outreach");

    [Fact]
    public void ACleanPositivePipelineDeclaresNoConflicts() =>
        D("""{"outreachStatus":"contacted","meetingAttended":true,"stillInterested":true}""").Conflicts.Should().BeEmpty();

    /* ------------------------------ the client's vocabulary, one case per row */

    private static readonly List<StatusOption> NewVocab = new[]
        {
            "New", "Attempted Contact", "Contacted", "Connected",
            "Contract Pending", "Awaiting Signature", "Signed", "Lost / Not Interested",
        }
        .Select((name, i) => new StatusOption($"s{i}", name, i))
        .ToList();

    private static string? StatusFor(string json) => ResolveStatus(D(json).Stage, NewVocab)?.Name;

    [Fact]
    public void ClientTableNewWhenNothingIsRecordedYet() => StatusFor("{}").Should().Be("New");

    [Fact]
    public void ClientTableAttemptedContact() => StatusFor("""{"outreachStatus":"attempted_no_answer"}""").Should().Be("Attempted Contact");

    [Fact]
    public void ClientTableContacted()
    {
        StatusFor("""{"outreachStatus":"contacted"}""").Should().Be("Contacted");
        // Their table has no meeting-scheduled status, so a booking must not claim Connected.
        StatusFor("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled"}""").Should().Be("Contacted");
    }

    [Fact]
    public void ClientTableConnectedMeansTheyShowedUp() => StatusFor("""{"meetingAttended":true}""").Should().Be("Connected");

    [Fact]
    public void ClientTableContractPending()
    {
        StatusFor("""{"meetingAttended":true,"stillInterested":true}""").Should().Be("Contract Pending");
        StatusFor("""{"contractStatus":"to_be_sent"}""").Should().Be("Contract Pending");
    }

    [Fact]
    public void ClientTableAwaitingSignature()
    {
        StatusFor("""{"contractStatus":"yes","contractSentDate":"2026-08-22"}""").Should().Be("Awaiting Signature");
        StatusFor("""{"contractSigned":"pending"}""").Should().Be("Awaiting Signature");
    }

    [Fact]
    public void ClientTableSignedAndAPaidDepositHoldsThere()
    {
        StatusFor("""{"contractSigned":"yes"}""").Should().Be("Signed");
        StatusFor("""{"contractSigned":"yes","depositStatus":"paid"}""").Should().Be("Signed");
        StatusFor("""{"depositStatus":"paid"}""").Should().Be("Signed");
    }

    [Fact]
    public void ClientTableLostOnEveryTriggerTheyListed()
    {
        StatusFor("""{"contactOutcome":"not_interested"}""").Should().Be("Lost / Not Interested");
        StatusFor("""{"meetingAttended":true,"stillInterested":false}""").Should().Be("Lost / Not Interested");
        StatusFor("""{"contractStatus":"no_longer_interested"}""").Should().Be("Lost / Not Interested");
        // Unqualified is a terminal that never degrades; without the combined label on
        // both terminals a rejected profile would resolve to nothing.
        StatusFor("""{"contractStatus":"profile_rejected"}""").Should().Be("Lost / Not Interested");
    }

    [Fact]
    public void ClientTableNeverInventsALabelOutsideTheOrgList()
    {
        var allowed = NewVocab.Select(o => o.Name).ToHashSet();
        foreach (var json in CategoricalStates)
        {
            var name = StatusFor(json);
            if (name != null) allowed.Should().Contain(name, json);
        }
    }

    [Fact]
    public void ClientTableIsMonotoneAcrossTheHappyPath()
    {
        var order = NewVocab.Select(o => o.Name).ToList();
        var journey = new[]
        {
            "{}",
            """{"outreachStatus":"attempted_no_answer"}""",
            """{"outreachStatus":"contacted"}""",
            """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-08-20"}""",
            """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-08-20","meetingAttended":true}""",
            """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-08-20","meetingAttended":true,"stillInterested":true}""",
            """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-08-20","meetingAttended":true,"stillInterested":true,"contractStatus":"yes","contractSentDate":"2026-08-22"}""",
            """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-08-20","meetingAttended":true,"stillInterested":true,"contractStatus":"yes","contractSentDate":"2026-08-22","contractSigned":"yes","signatureDate":"2026-08-25"}""",
            """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-08-20","meetingAttended":true,"stillInterested":true,"contractStatus":"yes","contractSentDate":"2026-08-22","contractSigned":"yes","signatureDate":"2026-08-25","depositStatus":"paid","paymentDate":"2026-08-28"}""",
        };
        var last = -1;
        foreach (var json in journey)
        {
            var at = order.IndexOf(StatusFor(json)!);
            at.Should().BeGreaterThanOrEqualTo(last, json);
            last = at;
        }
    }

    [Fact]
    public void ClientTableStillWorksForAnOrganisationLeftOnTheOldVocabulary()
    {
        ResolveStatus(Stage.MeetingHeld, DefaultOpts)?.Name.Should().NotBeNullOrEmpty();
        ResolveStatus(Stage.Signed, DefaultOpts)?.Name.Should().NotBeNullOrEmpty();
        ResolveStatus(Stage.Lost, DefaultOpts)!.Name.Should().Be("Lost");
    }

    /* ---------------------------- the status is derived: nobody pins it by hand */

    [Fact]
    public void EveryRecordedStepMovesTheStatusInEitherDirection()
    {
        var steps = new (string Json, string Stage)[]
        {
            ("""{"outreachStatus":"attempted_no_answer"}""", Stage.Attempted),
            ("""{"outreachStatus":"contacted"}""", Stage.Contacted),
            ("""{"contactOutcome":"meeting_scheduled"}""", Stage.MeetingScheduled),
            ("""{"meetingAttended":true}""", Stage.MeetingHeld),
            ("""{"stillInterested":true}""", Stage.Qualified),
            ("""{"contractStatus":"to_be_sent"}""", Stage.ContractPending),
            ("""{"contractStatus":"yes"}""", Stage.ContractSent),
            ("""{"contractSigned":"yes"}""", Stage.Signed),
            ("""{"depositStatus":"paid"}""", Stage.DepositPaid),
        };
        foreach (var (json, stage) in steps)
        {
            PlanFor(json, "New").Kind.Should().Be(PlanKind.Apply, $"forward to {stage}");
            // Backwards it may be a no-op ("signed" resolves to "Open Deal" here) but
            // never a suggestion: that was the old forward-only rule, which made an
            // undo appear to do nothing.
            var back = PlanFor(json, "Open Deal");
            back.Kind.Should().NotBe(PlanKind.Suggest, $"back to {stage}");
            if (back.Kind == PlanKind.Skip) back.Reason.Should().Be(SkipReason.Noop, $"back to {stage}");
        }
    }

    [Fact]
    public void EveryFailureMovesTheStatusAtEveryPhase()
    {
        foreach (var json in new[]
                 {
                     """{"contactOutcome":"not_interested"}""",
                     """{"meetingAttended":true,"stillInterested":false}""",
                     """{"contractStatus":"no_longer_interested"}""",
                     """{"contractStatus":"profile_rejected"}""",
                     """{"contractSigned":"no"}""",
                     """{"depositStatus":"not_paid"}""",
                 })
        {
            var plan = PlanFor(json, "Open Deal");
            plan.Kind.Should().Be(PlanKind.Apply, json);
            new[] { Stage.Lost, Stage.Unqualified }.Should().Contain(plan.To!.Canonical, json);
        }
    }

    [Fact]
    public void HoldsOnlyForAStatusThatIsNotAStageAtAll()
    {
        PlanFor("""{"stillInterested":true}""", "Do Not Contact", Opts(Fallback.Append("Do Not Contact").ToArray()))
            .Reason.Should().Be(SuggestReason.StickyStatus);
        PlanFor("""{"stillInterested":true}""", "Cold Outreach Q3", Opts(Fallback.Append("Cold Outreach Q3").ToArray()))
            .Reason.Should().Be(SuggestReason.UnrecognisedStatus);
    }

    [Fact]
    public void DoesNotTouchALeadWithNoPipelineActivityAtAll() =>
        PlanFor("{}", "New").Reason.Should().Be(SkipReason.NoSignal);

    [Fact]
    public void TheOrgCanStillShapeTheLadderByPinningStages()
    {
        var plan = PlanFor("""{"stillInterested":true}""", "New", Opts(Fallback.Append("Ready for contract").ToArray()),
            overrides: new Dictionary<string, string> { [Stage.Qualified] = "Ready for contract" });
        plan.Kind.Should().Be(PlanKind.Apply);
        plan.To!.Name.Should().Be("Ready for contract");
    }
}
