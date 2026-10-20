using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolScopeTests
{
    [Theory]
    [InlineData("file.read", ToolCapability.FileRead)]
    [InlineData("file.write", ToolCapability.FileWrite)]
    [InlineData("shell.run", ToolCapability.Process)]
    [InlineData("screen.capture", ToolCapability.ScreenCapture)]
    [InlineData("browser.navigate", ToolCapability.BrowserWrite)]
    [InlineData("mcp.other_server.read", ToolCapability.Network)]
    public async Task UnlistedToolsCannotReachAuthorizationApprovalOrExecutionEvenWithFullAccess(string name, ToolCapability capability)
    {
        var tool = new ScopedProbeTool(name, capability);
        var audit = new ToolProgressAuditSink();
        audit.Release.SetResult();
        var guard = new WorkspaceGuardProbe { Handler = (_, _) => throw new InvalidOperationException("Must not inspect workspace.") };
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) =>
            throw new InvalidOperationException("Must not ask approval.")), audit);
        var result = await executor.ExecuteAsync(Request(name), Context() with
        {
            AllowedToolNames = new HashSet<string> { "pull_request.get" },
            WorkspaceGuard = guard
        }, TestContext.Current.CancellationToken);
        Assert.Equal("tool_scope_denied", result.ErrorCode);
        Assert.Equal(0, guard.Checks);
        Assert.Equal(0, tool.Executions);
        Assert.Equal("tool_scope_denied", audit.Completion?.ErrorCode);
        Assert.False(audit.Completion?.IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllowedToolsStillRequireNormalApprovalAndCompleteAudit(bool approved)
    {
        var tool = new ScopedProbeTool("pull_request.comment", ToolCapability.Network, ToolRiskLevel.ExternalSideEffect);
        var approvals = 0;
        var audit = new ToolProgressAuditSink();
        audit.Release.SetResult();
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) =>
        { approvals++; return ValueTask.FromResult(approved); }), audit);
        var context = Context() with { ApprovalPolicy = ApprovalPolicy.AskEveryTime, AllowedToolNames = new HashSet<string> { tool.Descriptor.Name } };
        var result = await executor.ExecuteAsync(Request(tool.Descriptor.Name), context, TestContext.Current.CancellationToken);
        Assert.Equal(1, approvals);
        Assert.Equal(approved ? 1 : 0, tool.Executions);
        Assert.Equal(approved, result.IsSuccess);
        Assert.Equal(approved, audit.Completion?.IsSuccess);
        if (!approved) Assert.Equal("approval_denied", result.ErrorCode);
    }

    [Fact]
    public void ScopeSnapshotsCallerCollectionsAndSurvivesContextCopiesWithoutCaseOrComparerBypass()
    {
        var names = new HashSet<string> { "pull_request.get" };
        var context = Context() with { AllowedToolNames = names };
        names.Clear();
        names.Add("shell.run");
        var copy = context with { MaximumOutputCharacters = 1024 };
        Assert.True(copy.IsToolAllowed("PULL_REQUEST.GET"));
        Assert.False(copy.IsToolAllowed("shell.run"));
        Assert.Single(copy.AllowedToolNames!);
        Assert.Same(context.AllowedToolNames, copy.AllowedToolNames);
        Assert.False((copy with { ToolsEnabled = false }).IsToolAllowed("pull_request.get"));
        Assert.False((copy with { AllowedToolNames = new HashSet<string>() }).IsToolAllowed("pull_request.get"));
        Assert.True((copy with { AllowedToolNames = null }).IsToolAllowed("shell.run"));
    }

    [Fact]
    public async Task ScopeCannotReenableProjectlessToolsOrBypassSchemaAndRiskPolicy()
    {
        var tool = new ScopedProbeTool("fixture.write", ToolCapability.FileWrite, ToolRiskLevel.Mutation);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) =>
            throw new InvalidOperationException("Must not ask approval.")));
        var allowed = Context() with { AllowedToolNames = new HashSet<string> { tool.Descriptor.Name } };
        Assert.Equal("capability_denied", (await executor.ExecuteAsync(Request(tool.Descriptor.Name),
            allowed with { ToolsEnabled = false }, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal("capability_denied", (await executor.ExecuteAsync(Request(tool.Descriptor.Name),
            allowed with { ApprovalPolicy = ApprovalPolicy.ReadOnly }, TestContext.Current.CancellationToken)).ErrorCode);
        var invalid = Request(tool.Descriptor.Name) with { Arguments = JsonSerializer.SerializeToElement(new { unexpected = true }) };
        Assert.Equal("invalid_arguments", (await executor.ExecuteAsync(invalid,
            allowed with { AllowedToolNames = new HashSet<string>() }, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, tool.Executions);
    }

    [Fact]
    public async Task UnscopedExistingTasksAndCanonicalRegistryAliasesKeepTheirOriginalBehavior()
    {
        var tool = new ScopedProbeTool("fixture.read", ToolCapability.FileRead);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        Assert.True((await executor.ExecuteAsync(Request("FIXTURE.READ"), Context(), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await executor.ExecuteAsync(Request("FIXTURE.READ"), Context() with
        { AllowedToolNames = new HashSet<string> { "fixture.read" } }, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(2, tool.Executions);
    }

    private static ToolRequest Request(string name) => new("scope-fixture", name, JsonSerializer.SerializeToElement(new { }));
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), Path.GetTempPath(), [], ApprovalPolicy.FullAccess);
}
