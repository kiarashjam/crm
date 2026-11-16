using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Configuration;
using ACI.Infrastructure.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Tests;

/// <summary>
/// The assistant's model on OpenAI's chat completions: what it sends (the conversation, the
/// tools, the tool results) and how it reads the answer, without a real account.
/// </summary>
public class AssistantModelTests
{
    private static OpenAIAssistantModel Model(Func<HttpRequestMessage, string, HttpResponseMessage> answer, out List<JsonObject> sent, string key = "sk-test", bool enabled = true)
    {
        var bodies = new List<JsonObject>();
        sent = bodies;
        var handler = new Handler(async (request, ct) =>
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            bodies.Add((JsonObject)JsonNode.Parse(body)!);
            return answer(request, body);
        });
        return new OpenAIAssistantModel(
            new Factory(handler),
            Options.Create(new OpenAISettings { ApiKey = key, Model = "gpt-4o-mini" }),
            Options.Create(new AssistantOptions { Enabled = enabled, Temperature = 0.2 }),
            NullLogger<OpenAIAssistantModel>.Instance);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static AssistantModelRequest Request(params AssistantMessage[] messages) => new(
        "Answer from the tools.",
        messages,
        [new AssistantTool("list_leads", "List leads.", new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() })],
        500);

    [Fact]
    public async Task ItSendsTheInstructionsTheConversationAndTheToolsAsChatCompletionsTakesThem()
    {
        var model = Model((_, _) => Json("""{"choices":[{"message":{"role":"assistant","content":"Done."}}],"usage":{"prompt_tokens":10,"completion_tokens":3}}"""), out var sent);
        var call = new AssistantToolCall("call_1", "list_leads", new JsonObject { ["query"] = "Ada" });

        var reply = await model.CompleteAsync(Request(
            AssistantMessage.FromUser("Who is Ada?"),
            AssistantMessage.FromAssistant(null, [call]),
            AssistantMessage.ToolResult("call_1", "Leads: Ada Lovelace")));

        reply.Failed.Should().BeFalse();
        reply.Text.Should().Be("Done.");
        reply.InputTokens.Should().Be(10);
        reply.OutputTokens.Should().Be(3);

        var body = sent.Single();
        body["model"]!.GetValue<string>().Should().Be("gpt-4o-mini");
        body["max_completion_tokens"]!.GetValue<int>().Should().Be(500, "newer models refuse max_tokens");
        body.ContainsKey("max_tokens").Should().BeFalse();
        body["temperature"]!.GetValue<double>().Should().Be(0.2);
        var messages = body["messages"]!.AsArray();
        messages[0]!["role"]!.GetValue<string>().Should().Be("system");
        messages[0]!["content"]!.GetValue<string>().Should().Be("Answer from the tools.");
        messages[1]!["role"]!.GetValue<string>().Should().Be("user");
        messages[2]!["tool_calls"]![0]!["function"]!["name"]!.GetValue<string>().Should().Be("list_leads");
        messages[2]!["tool_calls"]![0]!["function"]!["arguments"]!.GetValue<string>().Should().Be("{\"query\":\"Ada\"}", "OpenAI takes the arguments as JSON in a string");
        messages[3]!["role"]!.GetValue<string>().Should().Be("tool");
        messages[3]!["tool_call_id"]!.GetValue<string>().Should().Be("call_1");
        body["tools"]![0]!["type"]!.GetValue<string>().Should().Be("function");
        body["tools"]![0]!["function"]!["name"]!.GetValue<string>().Should().Be("list_leads");
    }

    [Fact]
    public async Task ToolCallsAreReadWithTheirArguments_AndArgumentsThatAreNotAnObjectReadAsNone()
    {
        var model = Model((_, _) => Json("""
            {"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[
              {"id":"call_a","type":"function","function":{"name":"get_lead","arguments":"{\"id\":\"5\"}"}},
              {"id":"call_b","type":"function","function":{"name":"list_tasks","arguments":"not json"}}
            ]}}],"usage":{"prompt_tokens":7,"completion_tokens":2}}
            """), out _);

        var reply = await model.CompleteAsync(Request(AssistantMessage.FromUser("hi")));

        reply.Text.Should().BeNull();
        reply.ToolCalls.Should().HaveCount(2);
        reply.ToolCalls[0].Id.Should().Be("call_a");
        reply.ToolCalls[0].Arguments["id"]!.GetValue<string>().Should().Be("5");
        reply.ToolCalls[1].Name.Should().Be("list_tasks");
        reply.ToolCalls[1].Arguments.Count.Should().Be(0);
    }

    [Fact]
    public async Task ARefusalIsAReplyThatSaysSo_WithoutTheModelsOwnWords()
    {
        var model = Model((_, _) => Json("""{"error":{"message":"Incorrect API key provided: sk-test"}}""", HttpStatusCode.Unauthorized), out _);

        var reply = await model.CompleteAsync(Request(AssistantMessage.FromUser("hi")));

        reply.Failed.Should().BeTrue();
        reply.Error.Should().Be("The language model refused this server's key.");
        reply.Error.Should().NotContain("sk-test");
    }

    [Fact]
    public async Task WithoutAKeyOrWithTheSwitchOff_NothingIsSent()
    {
        var withoutKey = Model((_, _) => Json("{}"), out var sentWithoutKey, key: "");
        var switchedOff = Model((_, _) => Json("{}"), out var sentSwitchedOff, enabled: false);

        withoutKey.IsConfigured.Should().BeFalse();
        switchedOff.IsConfigured.Should().BeFalse();
        (await withoutKey.CompleteAsync(Request(AssistantMessage.FromUser("hi")))).Failed.Should().BeTrue();
        (await switchedOff.CompleteAsync(Request(AssistantMessage.FromUser("hi")))).Failed.Should().BeTrue();
        sentWithoutKey.Should().BeEmpty();
        sentSwitchedOff.Should().BeEmpty();
    }

    [Fact]
    public void AModelThatTakesNoTemperatureIsAskedWithoutOne()
    {
        var body = OpenAIAssistantModel.Body(Request(AssistantMessage.FromUser("hi")), "o4-mini", temperature: null);
        body.ContainsKey("temperature").Should().BeFalse();
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            answer(request, cancellationToken);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
