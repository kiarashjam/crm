using System.Text.Json.Nodes;

namespace ACI.Application.Interfaces;

/// <summary>
/// A language model the assistant talks to, behind one shape whatever the provider: the
/// instructions, the conversation and the tools it may call go in; an answer, or the tool
/// calls it wants made, comes out.
/// </summary>
/// <remarks>
/// <para>
/// OpenAI's chat completions is the first implementation, because the AI writer already
/// calls OpenAI with the same key (OpenAI:ApiKey), so no new account is needed. Nothing in
/// this shape is OpenAI's own: a message is a role and its text, a tool is a name, a
/// description and a JSON schema, and a call is a name and its arguments, which is also what
/// Anthropic's Messages API takes (tool_use and tool_result blocks). Another provider is one
/// more class behind this interface and a value of Assistant:Provider.
/// </para>
/// <para>
/// The model never acts. Its tool calls are carried out by the assistant's own code, as the
/// signed-in person, and a call that would change something only proposes the change for the
/// person to confirm (AssistantTools).
/// </para>
/// </remarks>
public interface IAssistantModel
{
    /// <summary>Whether a model is set up: a provider chosen and its key present. Nothing is sent anywhere when it is not.</summary>
    bool IsConfigured { get; }

    /// <summary>Who answers, as the settings page and the privacy notice name them ("OpenAI").</summary>
    string Provider { get; }

    /// <summary>The model asked, e.g. "gpt-4o-mini".</summary>
    string Model { get; }

    /// <summary>
    /// One turn of the model: what it says next, or the tools it wants called first. Never
    /// throws for a refusal or an outage: a reply that failed says why in <see cref="AssistantModelReply.Error"/>.
    /// </summary>
    Task<AssistantModelReply> CompleteAsync(AssistantModelRequest request, CancellationToken ct = default);
}

/// <summary>Who said a message in the conversation sent to the model.</summary>
public enum AssistantRole
{
    /// <summary>The person, or text the assistant wraps for them (fenced record data).</summary>
    User,
    /// <summary>The model's own earlier answer, with the tool calls it asked for.</summary>
    Assistant,
    /// <summary>What a tool call returned, answering the call it names.</summary>
    Tool,
}

/// <summary>One message of the conversation sent to the model.</summary>
/// <param name="ToolCalls">On an assistant message: the tool calls it asked for.</param>
/// <param name="ToolCallId">On a tool message: the call it answers.</param>
public sealed record AssistantMessage(
    AssistantRole Role,
    string? Text,
    IReadOnlyList<AssistantToolCall>? ToolCalls = null,
    string? ToolCallId = null)
{
    public static AssistantMessage FromUser(string text) => new(AssistantRole.User, text);

    public static AssistantMessage FromAssistant(string? text, IReadOnlyList<AssistantToolCall>? calls = null) =>
        new(AssistantRole.Assistant, text, calls);

    public static AssistantMessage ToolResult(string callId, string text) => new(AssistantRole.Tool, text, null, callId);
}

/// <summary>A tool the model may call: its name, what it does, and its arguments as a JSON schema (an object).</summary>
public sealed record AssistantTool(string Name, string Description, JsonObject Parameters);

/// <summary>A call the model asked for. <paramref name="Arguments"/> is empty when it sent none, or sent something that was not an object.</summary>
public sealed record AssistantToolCall(string Id, string Name, JsonObject Arguments);

/// <summary>Everything one turn of the model needs.</summary>
/// <param name="Instructions">The system prompt: the rules it answers by.</param>
/// <param name="MaxOutputTokens">The most it may write in this turn.</param>
public sealed record AssistantModelRequest(
    string Instructions,
    IReadOnlyList<AssistantMessage> Messages,
    IReadOnlyList<AssistantTool> Tools,
    int MaxOutputTokens);

/// <summary>What the model said: text, tool calls, or both; and what the turn cost in tokens.</summary>
/// <param name="Error">Why the turn failed (no answer, refused, timed out), in words fit for the person; null when it answered.</param>
public sealed record AssistantModelReply(
    string? Text,
    IReadOnlyList<AssistantToolCall> ToolCalls,
    int InputTokens,
    int OutputTokens,
    string? Error = null)
{
    public bool Failed => Error != null;

    public static AssistantModelReply Failure(string error, int inputTokens = 0, int outputTokens = 0) =>
        new(null, [], inputTokens, outputTokens, error);
}
