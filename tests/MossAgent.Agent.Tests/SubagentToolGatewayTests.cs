using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentToolGatewayTests
{
    [Fact]
    public async Task ChildCannotReplaceOrDropTheParentsSourceScope()
    {
        var parent = Context() with { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo") };
        var catalog = new DescriptorProbeToolCatalog([Descriptor("source.pr_get"), Descriptor("file.read")]);
        ToolExecutionContext? forwarded = null;
        var gateway = new SubagentToolGateway(catalog, new ToolProgressProbeExecutor((_, context, _) =>
        { forwarded = context; return Task.FromResult(ToolResult.Success("ok")); }), parent);
        Assert.Equal("source.pr_get", Assert.Single(gateway.Descriptors).Name);
        await gateway.ExecuteAsync(Request("source.pr_get"), Context() with
        { SourceScope = parent.SourceScope with { AccountId = Guid.NewGuid(), Repository = "other" } }, TestContext.Current.CancellationToken);
        Assert.Equal(parent.SourceScope, forwarded!.SourceScope);
        Assert.False(forwarded.IsToolAllowed("file.read"));
        Assert.True(forwarded.IsToolAllowed("SOURCE.PR_GET"));
    }

    [Fact]
    public void Descriptors_KeepOrderDuplicatesAndPublishedSnapshotsWithoutDelegation()
    {
        var first = Descriptor("test.first");
        var second = Descriptor("test.second");
        var catalog = new DescriptorProbeToolCatalog([first, Descriptor("SUBAGENT.RUN"), second, first]);
        var gateway = new SubagentToolGateway(catalog, RejectExecutor());

        var original = gateway.Descriptors;
        Assert.Equal(new[] { first, second, first }, original);
        Assert.Same(first, original[0]);
        catalog.Snapshot = [second];

        Assert.Equal(new[] { second }, gateway.Descriptors);
        Assert.Equal(new[] { first, second, first }, original);
        Assert.Equal(2, catalog.Reads);
    }

    [Theory]
    [InlineData("subagent.run")]
    [InlineData("SUBAGENT.RUN")]
    [InlineData("SubAgent.Run")]
    [InlineData("missing.tool")]
    public async Task ExecuteAsync_RejectsUnavailableToolsWithoutCallingTheExecutor(string name)
    {
        var catalog = new DescriptorProbeToolCatalog([Descriptor("test.read"), Descriptor("subagent.run")]);
        var gateway = new SubagentToolGateway(catalog, RejectExecutor());

        var result = await gateway.ExecuteAsync(Request(name), Context(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_tool_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesTaskStateAndStripsOnlyChildDelegationState()
    {
        var catalog = new DescriptorProbeToolCatalog([Descriptor("test.read")]);
        var request = Request("TEST.READ");
        var result = ToolResult.Success("completed", "content");
        var context = Context();
        ToolExecutionContext? forwarded = null;
        var executor = new ToolProgressProbeExecutor((received, childContext, token) =>
        {
            Assert.Same(request, received);
            Assert.Equal(TestContext.Current.CancellationToken, token);
            forwarded = childContext;
            return Task.FromResult(result);
        });
        var gateway = new SubagentToolGateway(catalog, executor);
        context = context with { SubagentTools = gateway };

        Assert.Same(result, await gateway.ExecuteAsync(request, context, TestContext.Current.CancellationToken));
        Assert.Equal(context with { ConversationContext = null, SubagentTools = null, SubagentProgress = null }, forwarded);
        Assert.Same(context.FileSnapshots, forwarded!.FileSnapshots);
        Assert.Same(context.Progress, forwarded.Progress);
        Assert.Same(gateway, context.SubagentTools);
        Assert.Equal("parent context", context.ConversationContext);
        Assert.Equal(1, catalog.Reads);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCurrentCatalogAfterRemovalAndRegistration()
    {
        var calls = 0;
        var catalog = new DescriptorProbeToolCatalog([Descriptor("test.read")]);
        var executor = new ToolProgressProbeExecutor((_, _, _) =>
        {
            calls++;
            return Task.FromResult(ToolResult.Success("completed"));
        });
        var gateway = new SubagentToolGateway(catalog, executor);
        var token = TestContext.Current.CancellationToken;

        Assert.True((await gateway.ExecuteAsync(Request("test.read"), Context(), token)).IsSuccess);
        catalog.Snapshot = [Descriptor("test.new")];
        Assert.Equal("subagent_tool_unavailable",
            (await gateway.ExecuteAsync(Request("test.read"), Context(), token)).ErrorCode);
        Assert.True((await gateway.ExecuteAsync(Request("test.new"), Context(), token)).IsSuccess);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_StillUsesTheStandardApprovalAndOutputPipeline(bool approved)
    {
        var tool = new CountingAgentTool();
        var registry = new ToolRegistry([tool]);
        var approval = new FixedToolApprovalService(approved);
        var gateway = new SubagentToolGateway(registry, new ToolExecutor(registry, approval));

        var result = await gateway.ExecuteAsync(Request(tool.Descriptor.Name),
            Context() with { MaximumOutputCharacters = 8 }, TestContext.Current.CancellationToken);

        Assert.Same(tool.Descriptor, approval.LastDescriptor);
        Assert.Equal(approved ? 1 : 0, tool.Calls);
        Assert.Equal(approved, result.IsSuccess);
        if (!approved) Assert.Equal("approval_denied", result.ErrorCode);
        Assert.InRange(result.OutputCharacterCount, 0, 8);
    }

    [Fact]
    public void ExecuteAsync_DoesNotAllocateAFilteredCatalogForMembershipChecks()
    {
        var descriptors = Enumerable.Range(0, 256).Select(index => Descriptor($"test.{index}")).ToArray();
        var catalog = new DescriptorProbeToolCatalog(descriptors);
        var completed = Task.FromResult(ToolResult.Success("completed"));
        var gateway = new SubagentToolGateway(catalog, new ToolProgressProbeExecutor((_, _, _) => completed));
        var request = Request(descriptors[0].Name);
        var context = Context();
        var token = TestContext.Current.CancellationToken;
        Action execute = () => GC.KeepAlive(gateway.ExecuteAsync(request, context, token));
        Action project = () => GC.KeepAlive(catalog.Descriptors
            .Where(static descriptor => !descriptor.Name.Equals(SubagentTool.ToolName, StringComparison.OrdinalIgnoreCase))
            .ToArray().Any(descriptor => descriptor.Name.Equals(request.ToolName, StringComparison.OrdinalIgnoreCase)));
        for (var index = 0; index < 32; index++) { execute(); project(); }

        var executionBytes = AllocatedBytes(execute);
        var projectionBytes = AllocatedBytes(project);

        Assert.True(executionBytes < projectionBytes / 2,
            $"Execution allocated {executionBytes} bytes; the redundant projection allocated {projectionBytes} bytes.");
    }

    [Fact]
    public async Task ParentAndChildScopesIntersectAndCannotBeWidenedThroughTheGateway()
    {
        var catalog = new DescriptorProbeToolCatalog([Descriptor("test.read"), Descriptor("shell.run")]);
        var parent = Context() with { AllowedToolNames = new HashSet<string> { "test.read" } };
        ToolExecutionContext? forwarded = null;
        var gateway = new SubagentToolGateway(catalog, new ToolProgressProbeExecutor((_, child, _) =>
        { forwarded = child; return Task.FromResult(ToolResult.Success("fixture")); }), parent);
        Assert.Equal("test.read", Assert.Single(gateway.Descriptors).Name);
        await gateway.ExecuteAsync(Request("test.read"), Context(), TestContext.Current.CancellationToken);
        Assert.True(forwarded!.IsToolAllowed("test.read"));
        Assert.False(forwarded.IsToolAllowed("shell.run"));
        await gateway.ExecuteAsync(Request("test.read"), Context() with { AllowedToolNames = new HashSet<string> { "shell.run" } }, TestContext.Current.CancellationToken);
        Assert.Empty(forwarded!.AllowedToolNames!);
        await gateway.ExecuteAsync(Request("test.read"), Context(), TestContext.Current.CancellationToken);
        Assert.True(forwarded!.IsToolAllowed("test.read"));
        Assert.Single(parent.AllowedToolNames!);
    }

    [Fact]
    public async Task ScopedGatewayStillDeniesThroughTheRealExecutionPipelineAndCannotReenableDisabledParent()
    {
        var tool = new CountingAgentTool();
        var registry = new ToolRegistry([tool]);
        var executor = new ToolExecutor(registry, new FixedToolApprovalService(true));
        var parent = Context() with { AllowedToolNames = new HashSet<string>() };
        var gateway = new SubagentToolGateway(registry, executor, parent);
        Assert.Empty(gateway.Descriptors);
        Assert.Equal("tool_scope_denied", (await gateway.ExecuteAsync(Request(tool.Descriptor.Name), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        gateway = new SubagentToolGateway(registry, executor, Context() with { ToolsEnabled = false });
        Assert.Empty(gateway.Descriptors);
        Assert.Equal("capability_denied", (await gateway.ExecuteAsync(Request(tool.Descriptor.Name), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, tool.Calls);
    }

    private static long AllocatedBytes(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 128; index++) action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static ToolDescriptor Descriptor(string name) =>
        new(name, name, """{"type":"object","properties":{}}""", ToolRiskLevel.ReadOnly, ToolCapability.FileRead);

    private static ToolRequest Request(string name) => new("child-call", name, JsonSerializer.SerializeToElement(new { }));

    private static ToolExecutionContext Context() => new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
        ApprovalPolicy.AskEveryTime, Timeout: TimeSpan.FromSeconds(10), ArtifactDirectory: "task-artifacts",
        ConversationContext: "parent context", SubagentProgress: _ => { })
    { Progress = _ => { } };

    private static IToolExecutor RejectExecutor() => new ToolProgressProbeExecutor((_, _, _) =>
        throw new InvalidOperationException("Unavailable tools must not reach the executor."));
}
