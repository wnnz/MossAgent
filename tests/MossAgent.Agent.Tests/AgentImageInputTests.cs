using System.Text.Json;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentImageInputTests
{
    [Fact]
    public async Task UserImages_SurviveToolRoundsWithoutMovingToToolOrSystemMessages()
    {
        var requests = new List<ModelRequest>();
        var fixture = Create(true, request =>
        {
            requests.Add(request);
            return requests.Count == 1
                ? [new ToolCallCompleted("call", "test.read", JsonSerializer.SerializeToElement(new { }))]
                : [new TextDelta("image processed"), new ModelCompleted()];
        });
        var image = fixture.Request.Messages[0].Images[0];
        var events = new List<AgentEvent>();

        await foreach (var item in fixture.Runner.RunAsync(fixture.Request, TestContext.Current.CancellationToken)) events.Add(item);

        Assert.Equal(2, requests.Count);
        foreach (var request in requests)
        {
            var user = Assert.Single(request.Messages, message => message.Role == ModelRole.User);
            Assert.Same(image, Assert.Single(user.Images));
            Assert.All(request.Messages.Where(message => message.Role != ModelRole.User),
                message => Assert.True(message.Images.IsDefaultOrEmpty));
        }
        Assert.Contains(requests[1].Messages, message => message.Role == ModelRole.Tool && message.ToolCallId == "call");
        Assert.Contains(events, item => item is AgentCompletedEvent);
        Assert.DoesNotContain(events, item => item is AgentFailureEvent);
    }

    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly, false)]
    [InlineData(ApprovalPolicy.AskEveryTime, false)]
    [InlineData(ApprovalPolicy.FullAccess, false)]
    [InlineData(ApprovalPolicy.ReadOnly, true)]
    [InlineData(ApprovalPolicy.AskEveryTime, true)]
    [InlineData(ApprovalPolicy.FullAccess, true)]
    public async Task PlainChat_ImagePayloadDoesNotGrantToolOrDirectoryAccess(ApprovalPolicy policy, bool unsolicitedCall)
    {
        var fixture = Create(false, _ => unsolicitedCall
            ? [new ToolCallCompleted("call", "test.read", JsonSerializer.SerializeToElement(new { }))]
            : [new TextDelta("image processed"), new ModelCompleted()]);
        var request = fixture.Request with { ToolContext = fixture.Request.ToolContext with { ApprovalPolicy = policy } };
        var events = new List<AgentEvent>();

        await foreach (var item in fixture.Runner.RunAsync(request, TestContext.Current.CancellationToken)) events.Add(item);

        Assert.Empty(fixture.Provider.LastRequest!.Tools);
        Assert.Empty(request.ToolContext.AuthorizedRoots);
        Assert.Empty(request.ToolContext.WorkingDirectory);
        Assert.Equal(0, fixture.Catalog.Reads);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Same(fixture.Request.Messages[0].Images[0],
            Assert.Single(fixture.Provider.LastRequest.Messages.Single(message => message.Role == ModelRole.User).Images));
        if (unsolicitedCall)
        {
            Assert.Equal("tools_disabled", Assert.Single(events.OfType<AgentFailureEvent>()).Code);
            Assert.DoesNotContain(events, item => item is AgentCompletedEvent);
        }
        else
        {
            Assert.DoesNotContain(events, item => item is AgentFailureEvent);
            Assert.Contains(events, item => item is AgentCompletedEvent);
        }
    }

    [Fact]
    public async Task UnsupportedModel_IsRejectedEvenForAnUnvalidatedProviderAdapter()
    {
        var fixture = Create(false, _ => throw new InvalidOperationException("Must not contact the provider."));
        var request = fixture.Request with { Model = fixture.Request.Model with { SupportsImages = false } };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var unused in fixture.Runner.RunAsync(request, TestContext.Current.CancellationToken)) { }
        });

        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Equal(0, fixture.Catalog.Reads);
    }

    private static (AgentRunner Runner, AgentRunRequest Request, StubModelProvider Provider,
        DescriptorProbeToolCatalog Catalog) Create(bool toolsEnabled, Func<ModelRequest, IReadOnlyList<ModelEvent>> response)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://fixture.invalid/"), "synthetic", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 32_000, 1_024,
            "off", null, null, true, false, true, true, "{}");
        var catalog = new DescriptorProbeToolCatalog([
            new("test.read", "fixture", "{}", ToolRiskLevel.ReadOnly, ToolCapability.FileRead)]);
        var executor = new ToolProgressProbeExecutor((_, _, _) => toolsEnabled
            ? Task.FromResult(ToolResult.Success("fixture"))
            : throw new InvalidOperationException("Plain chat must not execute tools."));
        var stream = new StubModelProvider(provider.Protocol, [], responseFactory: response);
        var runner = new AgentRunner(new StubModelProviderRegistry(stream), catalog, executor,
            new InMemorySubagentConfigurationRepository([]));
        var image = ModelImage.FromBytes("image/png", Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"));
        var context = new ToolExecutionContext(Guid.NewGuid(), toolsEnabled ? Path.GetTempPath() : "",
            toolsEnabled ? [Path.GetTempPath()] : [], ApprovalPolicy.ReadOnly) { ToolsEnabled = toolsEnabled };
        var request = new AgentRunRequest(provider, model, [new(ModelRole.User, "describe", Images: [image])], context);
        return (runner, request, stream, catalog);
    }
}
