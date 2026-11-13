using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Assistant;

/// <summary>
/// The assistant's model on OpenAI's chat completions API, with function calling: the same
/// account and key as the AI writer (OpenAI:ApiKey), so nothing new has to be signed up for.
/// </summary>
/// <remarks>
/// <para>
/// The HTTP client is made per call from the factory, with its own timeout
/// (Assistant:TimeoutSeconds): the writer's client lives inside a singleton for good, keeps its
/// first DNS answer, and waits the default 100 seconds for a reply nobody is still looking at.
/// </para>
/// <para>
/// It asks with max_completion_tokens, which every current model takes; max_tokens, which the
/// writer sends, is refused by the newer ones. A failure is a reply that says so, in words fit
/// for the person: the key, the model's raw error and the request stay in the server's log.
/// </para>
/// </remarks>
public sealed class OpenAIAssistantModel : IAssistantModel
{
    public const string HttpClientName = "OpenAIAssistant";
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

    private readonly IHttpClientFactory _http;
    private readonly OpenAISettings _openAi;
    private readonly AssistantOptions _options;
    private readonly ILogger<OpenAIAssistantModel> _logger;

    public OpenAIAssistantModel(
        IHttpClientFactory http,
        IOptions<OpenAISettings> openAi,
        IOptions<AssistantOptions> options,
        ILogger<OpenAIAssistantModel> logger)
    {
        _http = http;
        _openAi = openAi.Value;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.Enabled && _openAi.IsConfigured;

    public string Provider => "OpenAI";

    public string Model => string.IsNullOrWhiteSpace(_options.Model) ? _openAi.Model : _options.Model.Trim();

    public async Task<AssistantModelReply> CompleteAsync(AssistantModelRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return AssistantModelReply.Failure("No language model is set up on this server.");

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(Body(request, Model, _options.Temperature).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAi.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var client = _http.CreateClient(HttpClientName);
            using var response = await client.SendAsync(message, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("The assistant's model answered {Status}: {Body}", (int)response.StatusCode, Clip(text, 500));
                return AssistantModelReply.Failure((int)response.StatusCode switch
                {
                    401 or 403 => "The language model refused this server's key.",
                    429 => "The language model is busy or over its quota. Try again in a minute.",
                    _ => "The language model did not answer properly.",
                });
            }
            return Parse(text);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("The assistant's model did not answer within {Seconds} s", _options.TimeoutSeconds);
            return AssistantModelReply.Failure("The language model did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "The assistant's model could not be reached");
            return AssistantModelReply.Failure("The language model could not be reached.");
        }
    }

    /// <summary>The request body chat completions takes: the instructions first, then the conversation, then the tools.</summary>
    public static JsonObject Body(AssistantModelRequest request, string model, double? temperature)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = request.Instructions } };
        foreach (var m in request.Messages)
        {
            switch (m.Role)
            {
                case AssistantRole.User:
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = m.Text ?? "" });
                    break;
                case AssistantRole.Assistant:
                    var said = new JsonObject { ["role"] = "assistant", ["content"] = m.Text };
                    if (m.ToolCalls is { Count: > 0 } calls)
                    {
                        said["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode)new JsonObject
                        {
                            ["id"] = c.Id,
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = c.Name,
                                // OpenAI takes the arguments as JSON written into a string.
                                ["arguments"] = c.Arguments.ToJsonString(),
                            },
                        }).ToArray());
                    }
                    messages.Add(said);
                    break;
                case AssistantRole.Tool:
                    messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = m.ToolCallId, ["content"] = m.Text ?? "" });
                    break;
            }
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["max_completion_tokens"] = request.MaxOutputTokens,
        };
        if (temperature is double t) body["temperature"] = t;
        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(request.Tools.Select(tool => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.Parameters.DeepClone(),
                },
            }).ToArray());
            body["tool_choice"] = "auto";
        }
        return body;
    }

    /// <summary>Reads chat completions' answer: the first choice's text and tool calls, and the tokens it took.</summary>
    public static AssistantModelReply Parse(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return AssistantModelReply.Failure("The language model's answer could not be read.");
        }

        var usage = root?["usage"];
        var input = Int(usage?["prompt_tokens"]);
        var output = Int(usage?["completion_tokens"]);
        var message = root?["choices"]?[0]?["message"];
        if (message is null) return AssistantModelReply.Failure("The language model sent no answer.", input, output);

        var calls = new List<AssistantToolCall>();
        if (message["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var call in toolCalls)
            {
                var function = call?["function"];
                var name = Str(function?["name"]);
                if (string.IsNullOrWhiteSpace(name)) continue;
                calls.Add(new AssistantToolCall(Str(call?["id"]) ?? $"call_{calls.Count}", name, Arguments(function?["arguments"])));
            }
        }
        return new AssistantModelReply(Str(message["content"]), calls, input, output);
    }

    /// <summary>A call's arguments: JSON in a string, as OpenAI sends them. Anything that is not an object reads as none.</summary>
    private static JsonObject Arguments(JsonNode? node)
    {
        if (node is JsonObject obj) return (JsonObject)obj.DeepClone();
        var text = Str(node);
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        try
        {
            return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int Int(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}

/// <summary>
/// The assistant when no model is set up (no key, the switch off, or a provider this server
/// does not know): it says so and sends nothing anywhere, and the app keeps its built-in rules.
/// </summary>
public sealed class UnconfiguredAssistantModel : IAssistantModel
{
    public UnconfiguredAssistantModel(string provider) => Provider = provider;

    public bool IsConfigured => false;

    public string Provider { get; }

    public string Model => "";

    public Task<AssistantModelReply> CompleteAsync(AssistantModelRequest request, CancellationToken ct = default) =>
        Task.FromResult(AssistantModelReply.Failure("No language model is set up on this server."));
}
