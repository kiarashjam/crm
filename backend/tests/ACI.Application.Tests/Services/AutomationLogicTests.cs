using ACI.Application.Common;
using ACI.Application.Services;

namespace ACI.Application.Tests.Services;

/// <summary>
/// What a rule decides on its own: whether a record meets its conditions, where a run goes
/// next through branches, and when its emails may go.
/// </summary>
public class AutomationLogicTests
{
    private static readonly AutomationTrigger LeadCreated = AutomationCatalog.Find(AutomationCatalog.LeadCreated)!;
    private static readonly AutomationTrigger DealMoved = AutomationCatalog.Find(AutomationCatalog.DealStageChanged)!;

    private static AutomationCondition When(string field, string op, string? value = null, string[]? values = null, string? to = null) =>
        new() { Field = field, Op = op, Value = value, Values = values?.ToList(), To = to };

    private static ConditionGroup All(params AutomationCondition[] conditions) => new() { Match = "all", Conditions = conditions.ToList() };

    private static readonly Dictionary<string, FactValue> Qualified = new()
    {
        ["status"] = FactValue.Of("Qualified"),
        ["source"] = FactValue.Of("Website"),
        ["score"] = FactValue.OfNumber(72),
        ["owner"] = FactValue.OfId(null),
    };

    // ── conditions ───────────────────────────────────────────────────────────

    [Fact]
    public void NoConditions_HoldForEveryRecord()
    {
        AutomationConditions.Holds((ConditionGroup?)null, Qualified).Should().BeTrue();
        AutomationConditions.Holds(new ConditionGroup(), Qualified).Should().BeTrue("an empty group is no conditions");
    }

    [Fact]
    public void AllNeedsEveryCondition_AnyNeedsOne()
    {
        var both = All(When("status", "is", "qualified"), When("source", "is", "Referral"));
        AutomationConditions.Holds(both, Qualified).Should().BeFalse("the source is Website");
        both.Match = "any";
        AutomationConditions.Holds(both, Qualified).Should().BeTrue("the status is Qualified, in any letter case");
    }

    [Fact]
    public void AGroupInsideJoinsItsOwnConditions()
    {
        // Status is Qualified and (score at least 80 or source is Website).
        var rule = All(When("status", "is", "Qualified"));
        rule.Groups = [new ConditionGroup { Match = "any", Conditions = [When("score", "gte", "80"), When("source", "is", "Website")] }];
        AutomationConditions.Holds(rule, Qualified).Should().BeTrue();
        rule.Groups[0].Conditions[1].Value = "Referral";
        AutomationConditions.Holds(rule, Qualified).Should().BeFalse("72 is under 80 and the source is not Referral");
    }

    [Theory]
    [InlineData("gt", "70", null, true)]
    [InlineData("gt", "72", null, false)]
    [InlineData("gte", "72", null, true)]
    [InlineData("lt", "1'000", null, true)]
    [InlineData("between", "50", "80", true)]
    [InlineData("between", "73", "80", false)]
    [InlineData("eq", "72.00", null, true)]
    [InlineData("neq", "72", null, false)]
    public void NumbersCompareAsNumbers(string op, string value, string? to, bool holds) =>
        AutomationConditions.Holds(All(When("score", op, value, to: to)), Qualified).Should().Be(holds);

    [Fact]
    public void AnEmptyFieldMeetsIsEmptyAndNoComparison()
    {
        AutomationConditions.Holds(All(When("owner", "is_empty")), Qualified).Should().BeTrue();
        AutomationConditions.Holds(All(When("owner", "is", Guid.NewGuid().ToString())), Qualified).Should().BeFalse();
        AutomationConditions.Holds(All(When("value", "gte", "0")), Qualified).Should().BeFalse("a lead has no value, so it is not at least 0");
    }

    [Fact]
    public void AChoiceIsMatchedByItsIdOrItsName()
    {
        var stageId = Guid.NewGuid();
        var deal = new Dictionary<string, FactValue> { ["stage"] = FactValue.OfId(stageId, "Proposal") };
        AutomationConditions.Holds(All(When("stage", "is", stageId.ToString().ToUpperInvariant())), deal).Should().BeTrue();
        AutomationConditions.Holds(All(When("stage", "is", "proposal")), deal).Should().BeTrue();
        AutomationConditions.Holds(All(When("stage", "is_any_of", values: ["Won", "Proposal"])), deal).Should().BeTrue();
        AutomationConditions.Holds(All(When("stage", "is_none_of", values: ["Won", "Proposal"])), deal).Should().BeFalse();
    }

    [Fact]
    public void SavingRefusesAFieldTheTriggerDoesNotHave_AndAValueThatDoesNotFit()
    {
        AutomationConditions.Problem(All(When("status", "is", "New")), LeadCreated).Should().BeNull();
        AutomationConditions.Problem(All(When("stage", "is", "Won")), LeadCreated).Should().Contain("not something");
        AutomationConditions.Problem(All(When("score", "contains", "7")), LeadCreated).Should().Contain("cannot be tested");
        AutomationConditions.Problem(All(When("score", "gte", "lots")), LeadCreated).Should().Contain("number");
        AutomationConditions.Problem(All(When("status", "is", "  ")), LeadCreated).Should().Contain("Give a value");
        AutomationConditions.Problem(All(When("value", "between", "500", to: "100")), DealMoved).Should().Contain("smaller");
        var nested = All(When("status", "is", "New"));
        nested.Groups = [new ConditionGroup { Groups = [new ConditionGroup { Conditions = [When("status", "is", "New")] }] }];
        AutomationConditions.Problem(nested, LeadCreated).Should().Contain("cannot hold another group");
    }

    // ── tags and custom fields ───────────────────────────────────────────────

    private static ACI.Domain.Entities.CustomFieldDefinition Field(string entityType, string key, string type, params string[] options) => new()
    {
        Id = Guid.NewGuid(), EntityType = entityType, Key = key, Label = char.ToUpperInvariant(key[0]) + key[1..], Type = type,
        OptionsJson = options.Length == 0 ? null : System.Text.Json.JsonSerializer.Serialize(options),
    };

    private static readonly ACI.Domain.Entities.CustomFieldDefinition[] Fields =
    [
        Field("deal", "tier", "select", "Gold", "Silver"),
        Field("deal", "seats", "number"),
        Field("deal", "fee", "money"),
        Field("deal", "renewal", "date"),
        Field("deal", "corporate", "checkbox"),
        Field("deal", "interests", "multiselect", "Golf", "Wine"),
        Field("deal", "account", "link"),
        Field("lead", "budget", "money"),
        Field("member", "handicap", "number"),
    ];

    private static readonly AutomationChoice[] WorkspaceTags = [new("7f2b5c1e-0000-0000-0000-000000000001", "VIP"), new("7f2b5c1e-0000-0000-0000-000000000002", "Board")];

    [Fact]
    public void EachCustomFieldIsTestedAsTheKindOfValueItHolds_OnTheTriggersOfItsKindOfRecord()
    {
        var deal = AutomationCatalog.WithFlex(DealMoved, Fields, WorkspaceTags);
        AutomationField Of(string key) => deal.Fields.Single(f => f.Key == key);
        Of("tags").Type.Should().Be(AutomationFieldTypes.Choice);
        Of("tags").Choices!.Select(c => c.Label).Should().Equal("VIP", "Board");
        Of("cf:tier").Choices!.Select(c => c.Value).Should().Equal("Gold", "Silver");
        Of("cf:interests").Type.Should().Be(AutomationFieldTypes.Choice);
        Of("cf:seats").Type.Should().Be(AutomationFieldTypes.Number);
        Of("cf:fee").Type.Should().Be(AutomationFieldTypes.Money);
        Of("cf:renewal").Type.Should().Be(AutomationFieldTypes.Number, "a date is tested as the days from today to it");
        Of("cf:renewal").Label.Should().Contain("days from today");
        Of("cf:corporate").Type.Should().Be(AutomationFieldTypes.Bool);
        Of("cf:account").Type.Should().Be(AutomationFieldTypes.Text, "a link is tested by the linked record's name");
        deal.Fields.Should().NotContain(f => f.Key == "cf:budget" || f.Key == "cf:handicap", "those are a lead's and a member's");
        deal.Dates.Select(d => d.Key).Should().Contain(["expectedClose", "cf:renewal"]);
        deal.Fields.Take(DealMoved.Fields.Count).Should().Equal(DealMoved.Fields, "the trigger's own fields come first, as they were");

        var booking = AutomationCatalog.WithFlex(AutomationCatalog.Find(AutomationCatalog.ReservationCreated)!, Fields, WorkspaceTags);
        booking.Fields.Single(f => f.Key == "tags").Label.Should().Be("Member's tags", "a booking is tested by its member's tags");
        booking.Fields.Single(f => f.Key == "cf:handicap").Label.Should().Be("Member's Handicap");

        var task = AutomationCatalog.Find(AutomationCatalog.TaskOverdue)!;
        AutomationCatalog.WithFlex(task, Fields, WorkspaceTags).Should().BeSameAs(task, "a task carries no tags or custom fields");
        AutomationCatalog.FlexRecordType(AutomationRecords.Contract).Should().BeNull();
    }

    [Fact]
    public void ARecordsTagsAreTestedOneByOne_ByIdOrName()
    {
        var facts = new Dictionary<string, FactValue>
        {
            ["tags"] = FactValue.Of(WorkspaceTags[0].Value, "VIP", WorkspaceTags[1].Value, "Board"),
            ["cf:interests"] = FactValue.Of("Golf", "Wine"),
            ["cf:corporate"] = FactValue.OfFlag(false),
        };
        AutomationConditions.Holds(All(When("tags", "is", WorkspaceTags[0].Value)), facts).Should().BeTrue("it has VIP");
        AutomationConditions.Holds(All(When("tags", "is", "board")), facts).Should().BeTrue("by name too, ignoring case");
        AutomationConditions.Holds(All(When("tags", "is_not", "VIP")), facts).Should().BeFalse();
        AutomationConditions.Holds(All(When("tags", "is_none_of", values: ["Lapsed", "Guest"])), facts).Should().BeTrue();
        AutomationConditions.Holds(All(When("tags", "is_empty")), facts).Should().BeFalse();
        AutomationConditions.Holds(All(When("tags", "is_empty")), new Dictionary<string, FactValue>()).Should().BeTrue("a record with no tags");
        AutomationConditions.Holds(All(When("cf:interests", "is", "Wine")), facts).Should().BeTrue("one of the options picked");
        AutomationConditions.Holds(All(When("cf:corporate", "is_false")), facts).Should().BeTrue();
    }

    [Fact]
    public void SavingChecksATagOrFieldConditionAgainstTheWorkspacesFields()
    {
        var deal = AutomationCatalog.WithFlex(DealMoved, Fields, WorkspaceTags);
        AutomationConditions.Problem(All(When("cf:seats", "gte", "2"), When("tags", "is", "VIP")), deal).Should().BeNull();
        AutomationConditions.Problem(All(When("cf:seats", "gte", "2")), DealMoved).Should().Contain("not something", "without the workspace's fields");
        AutomationConditions.Problem(All(When("cf:budget", "gte", "2")), deal).Should().Contain("not something", "a lead's field");
        AutomationConditions.Problem(All(When("cf:tier", "contains", "Go")), deal).Should().Contain("cannot be tested");
        AutomationConditions.Problem(All(When("cf:seats", "gte", "many")), deal).Should().Contain("number");
        AutomationConditions.Describe(All(When("tags", "is_any_of", values: [WorkspaceTags[1].Value])), deal)
            .Should().Be("tags is any of Board", "the run log names a tag, not its id");
    }

    // ── steps ────────────────────────────────────────────────────────────────

    private static AutomationStep Step(string type) => new() { Id = Guid.NewGuid(), Type = type };

    private static AutomationStep Branch(List<AutomationStep> then, List<AutomationStep> otherwise) =>
        new() { Id = Guid.NewGuid(), Type = "branch", Condition = All(When("status", "is", "New")), Then = then, Else = otherwise };

    [Fact]
    public void ARunGoesThroughABranchAndOnToTheStepAfterIt()
    {
        // 0 task, 1 branch (yes: 1.t.0 email, 1.t.1 notify; no: nothing), 2 notify.
        var steps = new List<AutomationStep>
        {
            Step("create_task"),
            Branch([Step("send_email"), Step("notify")], []),
            Step("notify"),
        };
        AutomationSteps.Next(steps, "0").Should().Be("1");
        AutomationSteps.Enter(steps, "1", then: true).Should().Be("1.t.0");
        AutomationSteps.At(steps, "1.t.0")!.Type.Should().Be("send_email");
        AutomationSteps.Next(steps, "1.t.0").Should().Be("1.t.1");
        AutomationSteps.Next(steps, "1.t.1").Should().Be("2", "after the branch's last step comes the step after the branch");
        AutomationSteps.Enter(steps, "1", then: false).Should().Be("2", "a side with no steps goes straight on");
        AutomationSteps.Next(steps, "2").Should().BeNull("it was the last step");
        AutomationSteps.At(steps, "1.e.0").Should().BeNull();
        AutomationSteps.At(steps, "nonsense").Should().BeNull();
    }

    [Fact]
    public void ABranchInABranchComesBackOutLevelByLevel()
    {
        var steps = new List<AutomationStep> { Branch([Branch([], [Step("notify")])], []), Step("create_task") };
        AutomationSteps.Enter(steps, "0.t.0", then: false).Should().Be("0.t.0.e.0");
        AutomationSteps.Next(steps, "0.t.0.e.0").Should().Be("1");
        AutomationSteps.Depth(steps).Should().Be(2);
        AutomationSteps.Count(steps).Should().Be(4);
    }

    [Fact]
    public void RulesSavedBeforeStepsReadAsTheirActions()
    {
        var steps = AutomationSteps.Parse("""[{"id":"7c9e6679-7425-40de-944b-e07fc1f90ae7","type":"create_task","config":{"title":"Call","dueInDays":"1"}}]""");
        steps.Should().ContainSingle();
        steps[0].Type.Should().Be("create_task");
        steps[0].Config["title"].Should().Be("Call");
        AutomationSteps.Parse("not json").Should().BeEmpty();
    }

    // ── send windows ─────────────────────────────────────────────────────────

    private static readonly TimeZoneInfo Zurich = RegionalSettings.ZoneOf("Europe/Zurich");

    private static readonly SendWindow Weekdays = new() { Days = [1, 2, 3, 4, 5], Start = "09:00", End = "17:00" };

    private static DateTime ZurichTime(int year, int month, int day, int hour, int minute = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Zurich);

    [Fact]
    public void InsideTheWindowAnEmailGoesAtOnce()
    {
        var tuesdayMorning = ZurichTime(2026, 11, 10, 10, 30);
        SendWindows.NextOpening(Weekdays, Zurich, tuesdayMorning).Should().Be(tuesdayMorning);
        SendWindows.IsOpen(Weekdays, Zurich, tuesdayMorning).Should().BeTrue();
        SendWindows.NextOpening(null, Zurich, tuesdayMorning).Should().Be(tuesdayMorning, "no window: any time");
    }

    [Fact]
    public void OutsideTheWindowAnEmailWaitsForTheNextOpening_OnTheWorkspacesClock()
    {
        // Friday 18:00 in Zurich waits for Monday 09:00 in Zurich.
        SendWindows.NextOpening(Weekdays, Zurich, ZurichTime(2026, 11, 13, 18, 0)).Should().Be(ZurichTime(2026, 11, 16, 9, 0));
        // Before it opens today: today at nine.
        SendWindows.NextOpening(Weekdays, Zurich, ZurichTime(2026, 11, 11, 7, 15)).Should().Be(ZurichTime(2026, 11, 11, 9, 0));
        // At the minute it closes it is closed.
        SendWindows.NextOpening(Weekdays, Zurich, ZurichTime(2026, 11, 11, 17, 0)).Should().Be(ZurichTime(2026, 11, 12, 9, 0));
        // Nine in the morning is 08:00 UTC in winter and 07:00 UTC in summer.
        SendWindows.NextOpening(Weekdays, Zurich, ZurichTime(2026, 11, 14, 12, 0)).Should().Be(new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc));
        SendWindows.NextOpening(Weekdays, Zurich, ZurichTime(2026, 7, 4, 12, 0)).Should().Be(new DateTime(2026, 7, 6, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void AnOpeningInTheHourTheClocksSkipIsTakenWhenTheyLand()
    {
        // Sunday 29 March 2026: Zurich's clocks jump from 02:00 to 03:00.
        var night = new SendWindow { Days = [7], Start = "02:30", End = "05:00" };
        SendWindows.NextOpening(night, Zurich, ZurichTime(2026, 3, 28, 12, 0)).Should().Be(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc),
            "02:30 does not exist that night; 03:00 local, 01:00 UTC, is the first minute that does");
    }

    [Fact]
    public void AWindowMustOpenBeforeItClosesOnSomeDay()
    {
        SendWindows.Problem(Weekdays).Should().BeNull();
        SendWindows.Problem(new SendWindow { Days = [], Start = "09:00", End = "17:00" }).Should().Contain("at least one day");
        SendWindows.Problem(new SendWindow { Days = [8], Start = "09:00", End = "17:00" }).Should().Contain("1 (Monday) to 7 (Sunday)");
        SendWindows.Problem(new SendWindow { Days = [1], Start = "18:00", End = "08:00" }).Should().Contain("close after it opens");
        SendWindows.Problem(new SendWindow { Days = [1], Start = "nine", End = "17:00" }).Should().Contain("HH:mm");
        SendWindows.Describe(Weekdays).Should().Be("Mon–Fri 09:00–17:00");
    }
}
