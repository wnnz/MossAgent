using System.Text.Json;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentDescriptorProjectionTests
{
    [Fact]
    public async Task RunAsync_IsolatesTheCatalogSnapshotWhenDelegationIsEnabled()
    {
        ToolDescriptor[] snapshot = [Descriptor("test.read"), Descriptor(SubagentTool.ToolName)];
        var catalog = new DescriptorProbeToolCatalog(snapshot);
        var fixture = Create(catalog, true, _ => [new TextDelta("done")]);

        await ConsumeAsync(fixture.Runner, fixture.Request);

        Assert.NotSame(snapshot, fixture.Provider.LastRequest!.Tools);
        Assert.Equal(snapshot, fixture.Provider.LastRequest.Tools);
        Assert.Equal(1, catalog.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_InPlaceCatalogUpdatesDoNotRewriteEarlierRequests(bool enabled)
    {
        var first = Descriptor("test.first");
        var second = Descriptor("test.second");
        var delegation = Descriptor(SubagentTool.ToolName);
        var snapshot = new List<ToolDescriptor> { first, delegation };
        var catalog = new DescriptorProbeToolCatalog(snapshot);
        var requests = new List<ModelRequest>();
        var fixture = Create(catalog, enabled, request =>
        {
            requests.Add(request);
            snapshot.Clear();
            snapshot.Add(second);
            return requests.Count == 1
                ? [new ToolCallCompleted("tool-call", first.Name, JsonSerializer.SerializeToElement(new { }))]
                : [new TextDelta("done")];
        });

        await ConsumeAsync(fixture.Runner, fixture.Request);

        Assert.Equal(2, requests.Count);
        Assert.Equal(enabled ? new[] { first, delegation } : [first], requests[0].Tools);
        Assert.Equal(new[] { second }, requests[1].Tools);
        Assert.Equal(2, catalog.Reads);
    }

    [Fact]
    public async Task RunAsync_SupportsReadOnlyCollectionsWithoutListIndexing()
    {
        var first = Descriptor("test.read");
        var second = Descriptor(SubagentTool.ToolName);
        var catalog = new DescriptorProbeToolCatalog(new HashSet<ToolDescriptor> { first, second });
        var fixture = Create(catalog, true, _ => [new TextDelta("done")]);

        await ConsumeAsync(fixture.Runner, fixture.Request);

        Assert.Equal(catalog.Snapshot, fixture.Provider.LastRequest!.Tools);
        Assert.Equal(1, catalog.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_RefreshesDescriptorsEachTurnWithoutMutatingEarlierRequests(bool enabled)
    {
        var first = Descriptor("test.first");
        var second = Descriptor("test.second");
        var delegation = Descriptor("SUBAGENT.RUN");
        var catalog = new DescriptorProbeToolCatalog(new[] { first, delegation, first });
        var requests = new List<ModelRequest>();
        var fixture = Create(catalog, enabled, request =>
        {
            requests.Add(request);
            catalog.Snapshot = [second, delegation];
            return requests.Count == 1
                ? [new ToolCallCompleted("tool-call", first.Name, JsonSerializer.SerializeToElement(new { }))]
                : [new TextDelta("done")];
        });

        await ConsumeAsync(fixture.Runner, fixture.Request);

        Assert.Equal(2, requests.Count);
        Assert.Equal(enabled ? new[] { first, delegation, first } : [first, first], requests[0].Tools);
        Assert.Equal(enabled ? new[] { second, delegation } : [second], requests[1].Tools);
        Assert.Equal(2, catalog.Reads);
    }

    private static (AgentRunner Runner, AgentRunRequest Request, StubModelProvider Provider) Create(
        IToolCatalog catalog, bool enabled, Func<ModelRequest, IReadOnlyList<ModelEvent>> response)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.invalid/v1"), "synthetic-only", null, false, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
        var modelProvider = new StubModelProvider(provider.Protocol, [], responseFactory: response);
        var profile = new SubagentProfile(Guid.NewGuid(), "child", "fixture", "fixture", provider.Id,
            model.Id, "medium", false, enabled);
        var executor = new ToolProgressProbeExecutor((_, _, _) => Task.FromResult(ToolResult.Success("done")));
        var runner = new AgentRunner(new StubModelProviderRegistry(modelProvider), catalog, executor,
            new InMemorySubagentConfigurationRepository([profile]));
        var request = new AgentRunRequest(provider, model, [new ModelMessage(ModelRole.User, "fixture")],
            new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess));
        return (runner, request, modelProvider);
    }

    private static async Task ConsumeAsync(AgentRunner runner, AgentRunRequest request)
    {
        await foreach (var agentEvent in runner.RunAsync(request, TestContext.Current.CancellationToken))
            Assert.NotNull(agentEvent);
    }

    private static ToolDescriptor Descriptor(string name) =>
        new(name, name, """{"type":"object","properties":{}}""", ToolRiskLevel.ReadOnly, ToolCapability.FileRead);
}
