using System.Text.Json.Nodes;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// The test host with a language model that answers from a script, and the assistant's
/// settings (limits, throttles) set for the test.
/// </summary>
public sealed class AssistantFactory : CustomWebApplicationFactory
{
    private readonly Dictionary<string, string?> _settings;
    private readonly bool _paidCopy;

    /// <param name="settings">Configuration for the test, e.g. ["Assistant:DefaultMonthlyLimit"] = "2".</param>
    /// <param name="paidCopy">Whether the AI writer's drafts are calls to a paid model (counted against the month).</param>
    public AssistantFactory(Dictionary<string, string?>? settings = null, bool paidCopy = false)
    {
        _settings = settings ?? new();
        _paidCopy = paidCopy;
    }

    public ScriptedModel Model { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(_settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAssistantModel>();
            services.AddSingleton<IAssistantModel>(Model);
            if (_paidCopy)
            {
                services.RemoveAll<ICopyGenerator>();
                services.AddSingleton<ICopyGenerator, PaidCopyGenerator>();
            }
        });
    }
}

/// <summary>
/// A language model that answers from a script: each call gets the requests made so far, and
/// what it returns is the model's reply. Every request is kept, so a test can see exactly what
/// the model was sent (the tools offered, the record data, the fences around it).
/// </summary>
public sealed class ScriptedModel : IAssistantModel
{
    private readonly object _gate = new();

    public bool IsConfigured { get; set; } = true;

    public string Provider => "Test provider";

    public string Model => "scripted-1";

    public List<AssistantModelRequest> Requests { get; } = new();

    /// <summary>The reply to the n-th call (1-based); by default "Done." with no tool calls.</summary>
    public Func<AssistantModelRequest, int, AssistantModelReply> Answer { get; set; } = (_, _) => Say("Done.");

    public Task<AssistantModelReply> CompleteAsync(AssistantModelRequest request, CancellationToken ct = default)
    {
        int n;
        lock (_gate)
        {
            Requests.Add(request);
            n = Requests.Count;
        }
        return Task.FromResult(Answer(request, n));
    }

    public static AssistantModelReply Say(string text) => new(text, [], 120, 30);

    public static AssistantModelReply Call(string tool, JsonObject? arguments = null, string id = "call_1") =>
        new(null, [new AssistantToolCall(id, tool, arguments ?? new JsonObject())], 100, 20);

    /// <summary>The text of every tool result the model was sent in its last request.</summary>
    public IReadOnlyList<string> ToolResultsInLastRequest() =>
        Requests[^1].Messages.Where(m => m.Role == AssistantRole.Tool).Select(m => m.Text ?? "").ToList();

    /// <summary>The names of the tools the model was offered in its first request.</summary>
    public IReadOnlyList<string> ToolsOffered(int request = 0) => Requests[request].Tools.Select(t => t.Name).ToList();
}

/// <summary>An AI writer whose drafts are calls to a paid model, as OpenAI's are.</summary>
public sealed class PaidCopyGenerator : ICopyGenerator
{
    public bool CallsModel => true;

    public Task<string> GenerateAsync(string copyTypeId, string goal, string? context, string length, string? brandName, string? brandTone, CancellationToken ct = default) =>
        Task.FromResult("A drafted message.");

    public Task<string> RewriteAsync(string originalCopy, string adjustment, CancellationToken ct = default) =>
        Task.FromResult("A rewritten message.");

    public Task<GenerateCopyResult> GenerateWithRecipientAsync(string copyTypeId, string goal, string? context, string length, string? brandName, string? brandTone, RecipientContext? recipient, CancellationToken ct = default) =>
        Task.FromResult(new GenerateCopyResult("A drafted message.", "Hello"));

    public Task<GenerateCopyResult> GenerateInLanguageAsync(string copyTypeId, string goal, string? context, string length, string? brandName, string? brandTone, RecipientContext? recipient, string targetLanguage, CancellationToken ct = default) =>
        Task.FromResult(new GenerateCopyResult("Ein Entwurf.", "Hallo"));
}
