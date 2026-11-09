namespace ACI.Application.Common;

/// <summary>
/// What a logged activity can say beyond its text: when it happened, how a call or a
/// meeting went, how long it took, and who reached out to whom. One list of the words
/// each kind of activity can use, so the API, the browser's forms and anything that
/// reads them back (the first-response clock, the calendar) agree.
/// </summary>
/// <remarks>
/// A kind of activity only carries what applies to it. A note has no outcome and an
/// email no duration, so a value sent for one is refused rather than kept, where it
/// would say something nobody meant.
/// </remarks>
public static class ActivityFacts
{
    public const string Inbound = "inbound";
    public const string Outbound = "outbound";

    /// <summary>How a call went. "connected" is the only one in which anyone was spoken to.</summary>
    public static readonly IReadOnlyList<string> CallOutcomes =
        ["connected", "no_answer", "left_voicemail", "left_message", "busy", "wrong_number"];

    /// <summary>How a meeting went.</summary>
    public static readonly IReadOnlyList<string> MeetingOutcomes =
        ["completed", "no_show", "rescheduled", "cancelled"];

    public static readonly IReadOnlyList<string> Directions = [Inbound, Outbound];

    /// <summary>The longest call or meeting there is: a day.</summary>
    public const int MaxDurationMinutes = 24 * 60;

    /// <summary>
    /// How far ahead of the server's clock a time may be and still count as now: a
    /// browser's clock runs a little fast or slow.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Before this, a time is a typing mistake, not when something happened.</summary>
    public static readonly DateTime Earliest = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<string> OutcomesFor(string type) => type switch
    {
        "call" => CallOutcomes,
        "meeting" or "video" or "demo" => MeetingOutcomes,
        _ => [],
    };

    public static bool HasDirection(string type) => type is "call" or "email";

    public static bool HasDuration(string type) => type is "call" or "meeting" or "video" or "demo";

    /// <summary>"No answer", "no-answer" and "NO_ANSWER" are the same word. Blank is none.</summary>
    public static string? Word(string? value)
    {
        var text = value?.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>A time as UTC. A JSON time with no zone is taken as UTC, which is what the API writes.</summary>
    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };

    /// <summary>
    /// Whether this kind of activity can carry these values. <paramref name="outcome"/> and
    /// <paramref name="direction"/> are expected already read by <see cref="Word"/>.
    /// </summary>
    public static Result Check(string type, DateTime occurredAtUtc, string? outcome, int? durationMinutes, string? direction, DateTime nowUtc)
    {
        var time = CheckTime(occurredAtUtc, nowUtc);
        return time.IsFailure ? time : CheckFacts(type, outcome, durationMinutes, direction);
    }

    /// <summary>Whether this can be when something happened: not ahead of now, not before 2000.</summary>
    public static Result CheckTime(DateTime occurredAtUtc, DateTime nowUtc)
    {
        if (occurredAtUtc > nowUtc + ClockSkew) return DomainErrors.Activity.OccurredAhead;
        if (occurredAtUtc < Earliest) return DomainErrors.Activity.OccurredTooEarly;
        return Result.Success();
    }

    /// <summary>Whether this kind of activity can carry this outcome, duration and direction.</summary>
    public static Result CheckFacts(string type, string? outcome, int? durationMinutes, string? direction)
    {
        if (outcome != null)
        {
            var allowed = OutcomesFor(type);
            if (allowed.Count == 0) return DomainErrors.Activity.NoOutcome(type);
            if (!allowed.Contains(outcome)) return DomainErrors.Activity.InvalidOutcome(type, allowed);
        }

        if (durationMinutes != null)
        {
            if (!HasDuration(type)) return DomainErrors.Activity.NoDuration(type);
            if (durationMinutes < 0 || durationMinutes > MaxDurationMinutes) return DomainErrors.Activity.InvalidDuration;
        }

        if (direction != null)
        {
            if (!HasDirection(type)) return DomainErrors.Activity.NoDirection(type);
            if (!Directions.Contains(direction)) return DomainErrors.Activity.InvalidDirection;
        }

        return Result.Success();
    }

    /// <summary>
    /// Whether logging this was the team answering the person: what starts their
    /// "first response" and moves "last contacted". An email they sent us is not our
    /// answer, and nor is a call of theirs that nobody picked up.
    /// </summary>
    public static bool IsOurResponse(string type, string? direction, string? outcome) =>
        direction != Inbound || (type == "call" && outcome == "connected");
}
