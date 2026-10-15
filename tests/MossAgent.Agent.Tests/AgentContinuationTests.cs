using System.Text.Json;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentContinuationTests
{
    [Fact]
    public async Task ToolTurn_ForwardsOpaqueStateOnlyToTheNextModelRequest()
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.GeminiGenerateContent,
            new Uri("https://example.test/"), "synthetic-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "gemini-3", "fixture", 32000, 4096,
            "high", null, null, false, false, true, true, "{}");
        var continuation = new ModelContinuation(provider.Id, provider.Protocol, model.ModelId,
            JsonSerializer.SerializeToElement(new { parts = new[] { new { thoughtSignature = "opaque-secret" } } }));
        var requests = new List<ModelRequest>();
        var modelProvider = new StubModelProvider(provider.Protocol, [], responseFactory: request =>
        {
            requests.Add(request);
            return requests.Count == 1
                ? [new TextDelta("reading"), new ToolCallCompleted("internal-call", "test.read",
                    JsonSerializer.SerializeToElement(new { }), "native-call"), new ModelCompleted(Continuation: continuation)]
                : [new TextDelta("finished"), new ModelCompleted()];
        });
        var tool = new CountingAgentTool();
        var catalog = new ToolRegistry([tool]);
        var runner = new AgentRunner(new StubModelProviderRegistry(modelProvider), catalog,
            new ToolExecutor(catalog, new FixedToolApprovalService(true)), new InMemorySubagentConfigurationRepository([]));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);
        var events = new List<AgentEvent>();

        await foreach (var item in runner.RunAsync(new AgentRunRequest(provider, model, [new(ModelRole.User, "read")], context),
                           TestContext.Current.CancellationToken)) events.Add(item);

        Assert.Equal(2, requests.Count);
        var assistant = Assert.Single(requests[1].Messages, message => message.Role == ModelRole.Assistant);
        Assert.Same(continuation, assistant.Continuation);
        Assert.Equal("native-call", Assert.Single(assistant.ToolCalls!).ProviderCallId);
        Assert.Equal("internal-call", tool.LastRequest!.CallId);
        Assert.DoesNotContain("opaque-secret", tool.LastRequest.Arguments.GetRawText());
        Assert.All(events.OfType<AgentTextEvent>(), item => Assert.DoesNotContain("opaque-secret", item.Delta));
        Assert.Single(events.OfType<AgentCompletedEvent>());
    }
}
