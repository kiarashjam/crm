using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using static ACI.Application.Common.LeadStatusDerivation;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The server's status rule gives the same answer as the browser's, case for case.
/// </summary>
/// <remarks>
/// The browser (leadStatusSync.ts) still previews a status on the controls that
/// would change it; the server decides. Both run the inputs in
/// src/app/pages/leads/leadStatusSync.parity.json, build the same listing (one line
/// per case) and compare its SHA-256 with the digest stored there, so neither side
/// can change the rule alone. leadStatusSync.parity.test.ts is the other half.
/// Set LEAD_STATUS_PARITY_DUMP to a directory to get this side's listing for a diff.
/// </remarks>
public class LeadStatusParityTests
{
    private static readonly JsonElement Parity = Load();

    private static JsonElement Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src", "app", "pages", "leads", "leadStatusSync.parity.json")))
            dir = dir.Parent;
        if (dir == null) throw new FileNotFoundException("leadStatusSync.parity.json not found above " + AppContext.BaseDirectory);
        var text = File.ReadAllText(Path.Combine(dir.FullName, "src", "app", "pages", "leads", "leadStatusSync.parity.json"));
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    /// <summary>Every combination of the fields, first field outermost, as the JSON a lead would hold.</summary>
    private static List<string> Combinations(IReadOnlyList<(string Key, List<JsonElement> Values)> fields, int at = 0, List<string>? parts = null)
    {
        parts ??= new List<string>();
        if (at == fields.Count) return new List<string> { "{" + string.Join(",", parts) + "}" };
        var (key, values) = fields[at];
        var all = new List<string>();
        foreach (var v in values)
        {
            var next = v.ValueKind switch
            {
                JsonValueKind.Null => parts,
                JsonValueKind.String => parts.Append($"\"{key}\":\"{v.GetString()}\"").ToList(),
                _ => parts.Append($"\"{key}\":{(v.GetBoolean() ? "true" : "false")}").ToList(),
            };
            all.AddRange(Combinations(fields, at + 1, next));
        }
        return all;
    }

    private static (List<string> Derive, List<string> Plan) Listings()
    {
        var fields = Parity.GetProperty("fields").EnumerateObject()
            .Select(f => (f.Name, f.Value.EnumerateArray().ToList()))
            .ToList();
        var pipelines = Combinations(fields)
            .Concat(Parity.GetProperty("extraPipelines").EnumerateArray().Select(e => e.GetString()!))
            .ToList();

        var derive = new List<string>();
        var firstForRule = new List<(string Rule, string Json)>();
        foreach (var json in pipelines)
        {
            var d = DeriveStage(ParsePipeline(json));
            derive.Add(string.Join("\t", json, d.Stage, d.Phase, d.Terminal ? "true" : "false", d.Rule, d.Because, string.Join(";", d.Conflicts)));
            if (firstForRule.All(r => r.Rule != d.Rule)) firstForRule.Add((d.Rule, json));
        }

        var plan = new List<string>();
        foreach (var vocabulary in Parity.GetProperty("vocabularies").EnumerateArray())
        {
            var options = vocabulary.GetProperty("statuses").EnumerateArray()
                .Select(s => new StatusOption(s.GetProperty("id").GetString()!, s.GetProperty("name").GetString()!))
                .ToList();
            var overrides = vocabulary.TryGetProperty("overrides", out var o)
                ? o.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)
                : null;
            var currents = options.Select(s => s.Name)
                .Concat(Parity.GetProperty("extraStatuses").EnumerateArray().Select(e => e.GetString()!));
            foreach (var current in currents)
            {
                foreach (var (_, json) in firstForRule)
                {
                    var p = PlanStatusSync(new Context
                    {
                        Pipeline = ParsePipeline(json),
                        CurrentStatus = current,
                        StatusOptions = options,
                        StatusesLoaded = options.Count > 0,
                        StatusesUnavailable = options.Count == 0,
                        Overrides = overrides,
                    });
                    var to = p.Kind == PlanKind.Skip ? null : p.To;
                    plan.Add(string.Join("\t",
                        vocabulary.GetProperty("name").GetString(), current, json, p.Kind,
                        p.Kind == PlanKind.Apply ? "" : p.Reason,
                        p.Kind == PlanKind.Suggest ? string.Join(",", p.Reasons!) : "",
                        to?.Name ?? "", to?.Id ?? "", to?.Match ?? "", to?.Canonical ?? ""));
                }
            }
        }
        return (derive, plan);
    }

    private static string Digest(List<string> lines) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();

    private static void Dump(string name, List<string> lines)
    {
        var dir = Environment.GetEnvironmentVariable("LEAD_STATUS_PARITY_DUMP");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"cs-{name}.txt"), string.Join("\n", lines), new UTF8Encoding(false));
    }

    private static (int Cases, string Sha) Expected(string section)
    {
        var e = Parity.GetProperty("expected").GetProperty(section);
        return (e.GetProperty("cases").GetInt32(), e.GetProperty("sha256").GetString()!);
    }

    [Fact]
    public void DerivesTheSameStageAsTheBrowserForEveryReachablePipeline()
    {
        var (derive, _) = Listings();
        Dump("derive", derive);
        (derive.Count, Digest(derive)).Should().Be(Expected("derive"));
    }

    [Fact]
    public void MakesTheSameDecisionAsTheBrowserForEveryRuleVocabularyAndStartingStatus()
    {
        var (_, plan) = Listings();
        Dump("plan", plan);
        (plan.Count, Digest(plan)).Should().Be(Expected("plan"));
    }
}
