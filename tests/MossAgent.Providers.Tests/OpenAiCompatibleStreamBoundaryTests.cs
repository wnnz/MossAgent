using System.Net;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.OpenAi;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiCompatibleStreamBoundaryTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"synthetic-secret\"")]
    [InlineData("{\"choices\":[42]}")]
    [InlineData("{\"choices\":{}}")]
    [InlineData("{\"choices\":[{\"delta\":42}]}")]
    [InlineData("{\"choices\":[{\"delta\":{\"tool_calls\":[42]}}]}")]
    [InlineData("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":\"bad\"}]}}]}")]
    [InlineData("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"function\":42}]}}]}")]
    [InlineData("{synthetic-secret")]
    public async Task MalformedFrames_FailSafelyWithoutCompletionOrToolCalls(string frame)
    {
        var events = await CollectAsync($"data: {frame}\n\ndata: [DONE]\n\n");
        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_response", failure.Code);
        Assert.DoesNotContain("synthetic-secret", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrematureEof_DoesNotCompleteOrExecuteEvenValidPendingArguments(bool includeTool)
    {
        var body = Frame(new { id = "truncated", choices = new[] { new { index = 0, delta = new { content = "partial" } } } });
        if (includeTool) body += ToolDelta(0, "pending-call", "{}");
        var events = await CollectAsync(body);
        Assert.Equal("partial", Assert.IsType<TextDelta>(events[0]).Text);
        Assert.Equal("incomplete_response", Assert.IsType<ModelFailed>(events[1]).Code);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task AlternateChoices_CannotMergeTextOrToolCallsIntoTheSelectedAnswer()
    {
        var body = Frame(new
        {
            id = "choices",
            choices = new object[]
            {
                new { index = 1, delta = new { content = "other", tool_calls = new[] { new { index = 0, id = "wrong-call", function = new { name = "probe", arguments = "{\"wrong\":true}" } } } }, finish_reason = "tool_calls" },
                new { index = 0, delta = new { content = "selected", tool_calls = new[] { new { index = 0, id = "selected-call", function = new { name = "probe", arguments = "{\"selected\":true}" } } } }, finish_reason = "tool_calls" }
            }
        }) + "data: [DONE]\n\n";
        var events = await CollectAsync(body);
        Assert.Equal(3, events.Count);
        Assert.Equal("selected", Assert.IsType<TextDelta>(events[0]).Text);
        var call = Assert.IsType<ToolCallCompleted>(events[1]);
        Assert.Equal("selected-call", call.CallId);
        Assert.True(call.Arguments.GetProperty("selected").GetBoolean());
        Assert.False(call.Arguments.TryGetProperty("wrong", out _));
        Assert.Equal("choices", Assert.IsType<ModelCompleted>(events[2]).ResponseId);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("tool_calls")]
    public async Task ExplicitFinish_AllowsEofAndEmptyUsageWithoutDone(string finish)
    {
        var body = finish == "tool_calls" ? ToolDelta(0, "complete-call", "{}") : string.Empty;
        body += Frame(new { id = "finished", choices = new[] { new { index = 0, delta = new { content = "answer" }, finish_reason = finish } } });
        body += Frame(new { id = "finished", choices = Array.Empty<object>(), usage = new { completion_tokens = 5 } });
        var events = await CollectAsync(body);
        Assert.Equal("finished", Assert.IsType<ModelCompleted>(events[^1]).ResponseId);
        Assert.Single(events.OfType<TextDelta>());
        Assert.Equal(finish == "tool_calls" ? 1 : 0, events.OfType<ToolCallCompleted>().Count());
        Assert.Empty(events.OfType<ModelFailed>());
    }

    [Fact]
    public async Task InterleavedToolFragments_KeepIdentityArgumentsAndIndexOrder()
    {
        var body = ToolDelta(1, "second", "{\"value\":\"") + ToolDelta(0, "first", "{\"value\":\"");
        body += ToolDelta(1, null, "第二\"}") + ToolDelta(0, null, "第一\"}");
        var events = await CollectAsync(body + "data: [DONE]\n\n");
        var calls = events.OfType<ToolCallCompleted>().ToArray();
        Assert.Equal(["first", "second"], calls.Select(call => call.CallId));
        Assert.Equal(["第一", "第二"], calls.Select(call => call.Arguments.GetProperty("value").GetString()));
        Assert.Single(events.OfType<ModelCompleted>());
    }

    [Theory]
    [InlineData("{", "probe", "call")]
    [InlineData("{}", null, "call")]
    [InlineData("{}", "probe", null)]
    public async Task InvalidToolArguments_RejectWholeBatch(string arguments, string? name, string? callId)
    {
        var body = ToolDelta(0, "valid", "{}") + Frame(new
        {
            choices = new[] { new { delta = new { tool_calls = new[] { new { index = 1, id = callId, function = new { name, arguments } } } } } }
        }) + "data: [DONE]\n\n";
        var events = await CollectAsync(body);
        Assert.Equal("invalid_tool_arguments", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterText_DoesNotFlushPendingToolCalls(bool sameFrame)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var body = Frame(new { choices = new[] { new { delta = new { content = "first" } } } });
        body = sameFrame ? Frame(new
        {
            choices = new[] { new { delta = new { content = "first", tool_calls = new[] { new { index = 0, id = "pending", function = new { name = "probe", arguments = "{}" } } } }, finish_reason = "tool_calls" } }
        }) : body + ToolDelta(0, "pending", "{}");
        body += "data: [DONE]\n\n";
        var (provider, request) = CreateProvider(body);
        await using var iterator = provider.StreamAsync(request, cancellation.Token)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await iterator.MoveNextAsync());
        Assert.IsType<TextDelta>(iterator.Current);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await iterator.MoveNextAsync());
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("tool_calls")]
    public async Task IncompleteToolArguments_AtFinish_RejectTheWholeBatch(string finish)
    {
        var body = ToolDelta(0, "partial-call", "{\"unfinished\":")
            + Frame(new { choices = new[] { new { index = 0, delta = new { }, finish_reason = finish } } })
            + "data: [DONE]\n\n";
        var events = await CollectAsync(body);
        Assert.Equal("invalid_tool_arguments", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    private static async Task<List<ModelEvent>> CollectAsync(string body)
    {
        var (provider, request) = CreateProvider(body);
        var events = new List<ModelEvent>();
        await foreach (var item in provider.StreamAsync(request, TestContext.Current.CancellationToken)) events.Add(item);
        return events;
    }

    private static (OpenAiCompatibleProvider Provider, ModelRequest Request) CreateProvider(string body)
    {
        var handler = new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream");
        var clients = new StaticProviderHttpClientFactory(handler);
        var provider = new AiProvider(Guid.NewGuid(), "Fixture", ProviderProtocol.OpenAiCompatible,
            new Uri("https://example.test/v1/"), "synthetic-secret", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "Fixture", 8192, 1024,
            "off", null, null, false, false, true, true, "{}");
        var tool = new ToolDescriptor("probe", "fixture", "{\"type\":\"object\"}", ToolRiskLevel.ReadOnly, ToolCapability.None);
        return (new OpenAiCompatibleProvider(clients),
            new ModelRequest(provider, model, [new ModelMessage(ModelRole.User, "fixture")], [tool]));
    }

    private static string ToolDelta(int index, string? callId, string arguments) => Frame(new
    {
        choices = new[] { new { index = 0, delta = new { tool_calls = new[] { new { index, id = callId, function = new { name = callId is null ? null : "probe", arguments } } } } } }
    });

    private static string Frame(object payload) => $"data: {JsonSerializer.Serialize(payload)}\n\n";
}
