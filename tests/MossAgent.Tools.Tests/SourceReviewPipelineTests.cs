using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.SourceControl;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class SourceReviewPipelineTests
{
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), string.Empty, [], ApprovalPolicy.FullAccess)
    { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo", false) };
    private static ToolRequest Request(string name = "source.pr_list") => new("fixture", name, JsonSerializer.SerializeToElement(new { page = 1 }));

    [Theory]
    [InlineData("file.read", ToolCapability.FileRead)]
    [InlineData("file.write", ToolCapability.FileWrite)]
    [InlineData("shell.run", ToolCapability.Process)]
    [InlineData("screen.capture", ToolCapability.ScreenCapture)]
    [InlineData("browser.navigate", ToolCapability.BrowserWrite)]
    [InlineData("mcp.any.read", ToolCapability.Network)]
    public async Task SourceScopeCannotGrantOtherToolsEvenWithoutAllowlist(string name, ToolCapability capability)
    {
        var tool = new ScopedProbeTool(name, capability);
        var executor = new ToolExecutor(new([tool]), new SourceReviewApprovalProbe(() => Assert.Fail("No approval expected.")));
        var result = await executor.ExecuteAsync(new("blocked", name, JsonSerializer.SerializeToElement(new { })), Context(), TestContext.Current.CancellationToken);
        Assert.Equal("tool_scope_denied", result.ErrorCode);
        Assert.Equal(0, tool.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSourceScopeOrProjectlessToolsNeverContactSource(bool projectless)
    {
        var reader = new SourceReviewReaderProbe();
        var executor = new ToolExecutor(new([new PullRequestListTool(reader)]), new SourceReviewApprovalProbe(() => { }));
        var context = projectless ? Context() with { ToolsEnabled = false } : Context() with { SourceScope = null };
        var result = await executor.ExecuteAsync(Request(), context, TestContext.Current.CancellationToken);
        Assert.Equal(projectless ? "capability_denied" : "source_scope_denied", result.ErrorCode);
        Assert.Equal(0, reader.Reads);
    }

    [Fact]
    public async Task RevocationAfterApprovalStopsExecutionAndCompletesAudit()
    {
        var reader = new SourceReviewReaderProbe();
        var audit = new ToolProgressAuditSink(); audit.Release.SetResult();
        var approval = new SourceReviewApprovalProbe(() => reader.IsValid = false);
        var executor = new ToolExecutor(new([new PullRequestListTool(reader)]), approval, audit);
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.Equal("source_scope_denied", result.ErrorCode);
        Assert.Equal(2, reader.Validations);
        Assert.Equal(1, approval.Calls);
        Assert.Equal(0, reader.Reads);
        Assert.Equal("source_scope_denied", audit.Completion!.ErrorCode);
    }

    [Fact]
    public async Task SchemaApprovalAndOutputLimitsRemainEnforced()
    {
        var reader = new SourceReviewReaderProbe();
        var approval = new SourceReviewApprovalProbe(() => { }, false);
        var list = new PullRequestListTool(reader);
        var get = new PullRequestGetTool(reader);
        var executor = new ToolExecutor(new([list, get]), approval);
        Assert.False(list.Descriptor.IsModelVisible);
        Assert.Equal("invalid_arguments", (await executor.ExecuteAsync(Request() with
        { Arguments = JsonSerializer.SerializeToElement(new { page = 1, repository = "other" }) }, Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, reader.Validations);
        Assert.Equal("approval_denied", (await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, reader.Reads);
        var accepted = new ToolExecutor(new([get]), new SourceReviewApprovalProbe(() => { }));
        var result = await accepted.ExecuteAsync(new("get", "source.pr_get", JsonSerializer.SerializeToElement(new { number = 7 })),
            Context() with { MaximumOutputCharacters = 80 }, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.InRange(result.OutputCharacterCount, 1, 80);
    }

    [Fact]
    public async Task PreapprovalValidationHasCancellationAndDeadline()
    {
        var reader = new SourceReviewReaderProbe { OnValidate = token => Task.Delay(Timeout.Infinite, token) };
        var executor = new ToolExecutor(new([new PullRequestListTool(reader)]), new SourceReviewApprovalProbe(() => Assert.Fail("Not authorized.")));
        var result = await executor.ExecuteAsync(Request(), Context() with { Timeout = TimeSpan.FromMilliseconds(20) }, TestContext.Current.CancellationToken);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(0, reader.Reads);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.Cancel();
        result = await executor.ExecuteAsync(Request(), Context(), cancel.Token);
        Assert.Equal("tool_cancelled", result.ErrorCode);
    }
}
