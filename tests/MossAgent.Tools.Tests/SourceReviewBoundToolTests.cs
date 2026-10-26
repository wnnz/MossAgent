using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.SourceControl;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class SourceReviewBoundToolTests
{
    private static ToolExecutionContext Context => new(Guid.NewGuid(), string.Empty, [], ApprovalPolicy.ReadOnly)
    { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo", false), SourceReviewRevision = new(7, new string('a', 40), new string('b', 40)) };

    private static IAgentTool[] Tools(PullRequestInspectionProbe inspection, SourceReviewReaderProbe summaries) =>
        [new PullRequestGetTool(summaries), new PullRequestListTool(summaries), new PullRequestFilesTool(inspection),
         new PullRequestCommentsTool(inspection), new PullRequestChecksTool(inspection),
         new PullRequestReviewsTool(inspection), new PullRequestActivityTool(inspection)];

    private static ToolRequest Request(string name, int number = 7, char head = 'b')
    {
        var arguments = new Dictionary<string, object> { ["number"] = number };
        if (name != "source.pr_get")
        {
            arguments["base_revision"] = new string('a', 40); arguments["head_revision"] = new string(head, 40); arguments["page"] = 1;
            if (name is "source.pr_comments" or "source.pr_checks") arguments["kind"] = name == "source.pr_comments" ? "inline" : "runs";
        }
        return new("bound-call", name, JsonSerializer.SerializeToElement(arguments));
    }

    [Fact]
    public void OnlyBoundReadMetadataIsProjectedWithoutChangingGlobalVisibility()
    {
        var registry = new ToolRegistry(Tools(new(), new()));
        Assert.Empty(registry.Descriptors);
        var projected = registry.GetModelDescriptors(Context);
        Assert.Equal(new[] { "source.pr_checks", "source.pr_comments", "source.pr_files", "source.pr_get" }, projected.Select(tool => tool.Name).Order());
        Assert.All(projected, descriptor => Assert.True(descriptor.IsModelVisible));
        Assert.Empty(registry.Descriptors);
        Assert.Empty(registry.GetModelDescriptors(Context with { SourceScope = null }));
        Assert.Empty(registry.GetModelDescriptors(Context with { ToolsEnabled = false }));
    }

    [Theory]
    [InlineData("source.pr_get")]
    [InlineData("source.pr_files")]
    [InlineData("source.pr_comments")]
    [InlineData("source.pr_checks")]
    public async Task AnotherPrIsRefusedBeforeApprovalOrSourceReads(string name)
    {
        var inspection = new PullRequestInspectionProbe(); var summaries = new SourceReviewReaderProbe();
        var approval = new SourceReviewApprovalProbe(() => { });
        var executor = new ToolExecutor(new(Tools(inspection, summaries)), approval);

        var result = await executor.ExecuteAsync(Request(name, 8), Context, TestContext.Current.CancellationToken);

        Assert.Equal("source_scope_denied", result.ErrorCode);
        Assert.Null(result.Content); Assert.Equal(0, approval.Calls);
        Assert.Equal(0, inspection.Reads); Assert.Equal(0, summaries.Reads);
    }

    [Theory]
    [InlineData("source.pr_files")]
    [InlineData("source.pr_comments")]
    [InlineData("source.pr_checks")]
    public async Task ModelCannotChooseADifferentCommit(string name)
    {
        var inspection = new PullRequestInspectionProbe();
        var executor = new ToolExecutor(new(Tools(inspection, new())), new SourceReviewApprovalProbe(() => { }));

        var result = await executor.ExecuteAsync(Request(name, head: 'c'), Context, TestContext.Current.CancellationToken);

        Assert.Equal("source_scope_denied", result.ErrorCode); Assert.Equal(0, inspection.Reads);
    }

    [Fact]
    public async Task ChangedGetRevisionNeverReturnsNewSourceText()
    {
        var summaries = new SourceReviewReaderProbe();
        var executor = new ToolExecutor(new(Tools(new(), summaries)), new SourceReviewApprovalProbe(() => { }));

        var result = await executor.ExecuteAsync(Request("source.pr_get"), Context, TestContext.Current.CancellationToken);

        Assert.Equal("source_revision_changed", result.ErrorCode); Assert.Null(result.Content); Assert.Equal(1, summaries.Reads);
    }

    [Theory]
    [InlineData("source.pr_list")]
    [InlineData("source.pr_reviews")]
    [InlineData("source.pr_activity")]
    [InlineData("source.pr_write")]
    [InlineData("filesystem.read_file")]
    [InlineData("shell.run")]
    [InlineData("browser.navigate")]
    [InlineData("subagent.run")]
    [InlineData("mcp.source.pr_get")]
    public void BoundScopeDoesNotGrantOtherToolsEvenWithFullAccessOrNoAllowlist(string name)
    {
        Assert.False((Context with { ApprovalPolicy = ApprovalPolicy.FullAccess }).IsToolAllowed(name));
        Assert.False((Context with { SourceScope = null }).IsToolAllowed(name));
    }

    [Fact]
    public async Task BoundReadStillRequiresApprovalAndSupportsDeadlineAndOutputRefusal()
    {
        var inspection = new PullRequestInspectionProbe();
        var registry = new ToolRegistry(Tools(inspection, new()));
        var denied = new ToolExecutor(registry, new SourceReviewApprovalProbe(() => { }, false));
        Assert.Equal("approval_denied", (await denied.ExecuteAsync(Request("source.pr_files"), Context, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, inspection.Reads);
        var executor = new ToolExecutor(registry, new SourceReviewApprovalProbe(() => { }));
        Assert.Equal("source_output_limit", (await executor.ExecuteAsync(Request("source.pr_files"), Context with { MaximumOutputCharacters = 80 }, TestContext.Current.CancellationToken)).ErrorCode);
        inspection.OnRead = token => Task.Delay(Timeout.Infinite, token);
        Assert.Equal("tool_timeout", (await executor.ExecuteAsync(Request("source.pr_files"), Context with { Timeout = TimeSpan.FromMilliseconds(30) }, TestContext.Current.CancellationToken)).ErrorCode);
    }

    [Theory]
    [InlineData("source.pr_get")]
    [InlineData("source.pr_files")]
    [InlineData("source.pr_comments")]
    [InlineData("source.pr_checks")]
    public async Task RevocationDuringApprovalPreventsBoundReads(string name)
    {
        var inspection = new PullRequestInspectionProbe();
        var summaries = new SourceReviewReaderProbe();
        var approval = new SourceReviewApprovalProbe(() => { inspection.IsValid = false; summaries.IsValid = false; });
        var executor = new ToolExecutor(new(Tools(inspection, summaries)), approval);

        var result = await executor.ExecuteAsync(Request(name), Context, TestContext.Current.CancellationToken);

        Assert.Equal("source_scope_denied", result.ErrorCode); Assert.Null(result.Content);
        Assert.Equal(1, approval.Calls); Assert.Equal(0, inspection.Reads); Assert.Equal(0, summaries.Reads);
        Assert.Contains("仓库授权", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BoundGetRefusesOversizedJsonWhileLegacyGetStillUsesPipelineTruncation()
    {
        var summaries = new SourceReviewReaderProbe
        { Summary = new(7, new string('z', 400), "author", "open", false, "body", new string('a', 40), new string('b', 40)) };
        var executor = new ToolExecutor(new(Tools(new(), summaries)), new SourceReviewApprovalProbe(() => { }));
        var context = Context with { MaximumOutputCharacters = 160 };

        var bound = await executor.ExecuteAsync(Request("source.pr_get"), context, TestContext.Current.CancellationToken);
        var legacy = await executor.ExecuteAsync(Request("source.pr_get"), context with { SourceReviewRevision = null }, TestContext.Current.CancellationToken);

        Assert.False(bound.IsSuccess); Assert.Equal("source_output_limit", bound.ErrorCode); Assert.Null(bound.Content);
        Assert.Contains("输出上限", bound.Summary, StringComparison.Ordinal);
        Assert.True(legacy.IsSuccess); Assert.InRange(legacy.OutputCharacterCount, 1, 160);
    }
}
