using System.Text;
using System.Text.Json;

namespace ACI.Application.Common;

/// <summary>
/// Which of the five phases of the lead pipeline (outreach, meeting, contract, signature,
/// deposit) a lead is in, as the lead's sales tracker says it: "Currently in Phase 2: Meeting".
/// </summary>
/// <remarks>
/// <para>
/// The same rule as <c>phaseCompletion</c> and <c>currentPhase</c> in
/// <c>src/app/pages/leads/leadPipeline.ts</c>: a phase is done when its outcome is recorded with
/// its date (a meeting scheduled on a day, a contract sent on a day...), and a lead is in the first
/// phase not done; one with all five done waits at the last, the deposit, to be converted.
/// </para>
/// <para>
/// Whether a lead has dropped out is not decided here: that is the status rule's
/// (<see cref="LeadStatusDerivation"/>), whose terminal stages are the lost and the rejected.
/// </para>
/// </remarks>
public static class LeadPhases
{
    /// <summary>How many phases there are.</summary>
    public const int Count = 5;

    /// <summary>The 1-based phase a lead whose PipelineState is <paramref name="json"/> is in.</summary>
    public static int Current(string? json)
    {
        var done = Completion(json);
        var first = Array.IndexOf(done, false);
        return first < 0 ? Count : first + 1;
    }

    /// <summary>Whether each phase is done (index 0 is the first), as the tracker ticks them.</summary>
    public static bool[] Completion(string? json)
    {
        var fields = Read(json);
        string? Text(string name)
        {
            if (!fields.TryGetValue(name, out var v) || v.ValueKind != JsonValueKind.String) return null;
            try
            {
                return v.GetString();
            }
            catch (InvalidOperationException)
            {
                // Half a surrogate pair: never a value the rule compares against.
                return null;
            }
        }
        bool? Flag(string name) => fields.TryGetValue(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        bool Has(string name) => !string.IsNullOrEmpty(Text(name));

        // The legacy depositPaid boolean, folded as parsePipeline folds it: only when
        // depositStatus is absent altogether.
        var deposit = Text("depositStatus");
        if (!fields.ContainsKey("depositStatus") && Flag("depositPaid") is bool paid) deposit = paid ? "paid" : "pending";

        return
        [
            Text("outreachStatus") == "contacted" && Text("contactOutcome") == "meeting_scheduled" && Has("meetingDate"),
            Flag("meetingAttended") == true && Flag("stillInterested") == true,
            Text("contractStatus") == "yes" && Has("contractSentDate"),
            Text("contractSigned") == "yes" && Has("signatureDate"),
            deposit == "paid" && Has("paymentDate"),
        ];
    }

    /// <summary>The pipeline's fields, the last of a repeated key winning; none for anything that is not a JSON object.</summary>
    private static Dictionary<string, JsonElement> Read(string? json)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return fields;
        try
        {
            // Through UTF-8 with replacement, as the status rule reads it: a JavaScript string can
            // hold half of a surrogate pair, which a .NET string parse refuses outright.
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return fields;
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                try
                {
                    fields[property.Name] = property.Value.Clone();
                }
                catch (InvalidOperationException)
                {
                    // A name escaping half a surrogate pair: never one of the fields read here.
                }
            }
        }
        catch (JsonException)
        {
            fields.Clear();
        }
        return fields;
    }
}
