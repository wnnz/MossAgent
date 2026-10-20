using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.SourceControl;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class PullRequestInspectionPipelineTests
{
    private static ToolExecutionContext Context => new(Guid.NewGuid(), string.Empty, [], ApprovalPolicy.FullAccess)
    { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo") };
    private static ToolRequest Request(string name)
    {
        var arguments = new Dictionary<string, object> { ["number"] = 7, ["base_revision"] = new string('a', 40), ["head_revision"] = new string('b', 40), ["page"] = 1 };
        if (name != "source.pr_files") arguments["kind"] = name == "source.pr_comments" ? "inline" : "runs";
        return new("inspection", name, JsonSerializer.SerializeToElement(arguments));
    }
    private static IAgentTool[] Tools(PullRequestInspectionProbe probe) => [new PullRequestFilesTool(probe), new PullRequestCommentsTool(probe), new PullRequestChecksTool(probe)];

    [Theory]
    [InlineData("source.pr_files")]
    [InlineData("source.pr_comments")]
    [InlineData("source.pr_checks")]
    public async Task EachReadRequiresApprovalAndRevalidatesTheScope(string name)
    {
        var probe = new PullRequestInspectionProbe();
        var approval = new SourceReviewApprovalProbe(() => { }, false);
        var tools = Tools(probe);
        Assert.All(tools, tool => { Assert.False(tool.Descriptor.IsModelVisible); Assert.Equal(ToolRiskLevel.ReadOnly, tool.Descriptor.RiskLevel); });
        var executor = new ToolExecutor(new(tools), approval);
        Assert.Equal("approval_denied", (await executor.ExecuteAsync(Request(name), Context, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(1, probe.Validations); Assert.Equal(0, probe.Reads);
        var revoke = new SourceReviewApprovalProbe(() => probe.IsValid = false);
        var audit = new ToolProgressAuditSink(); audit.Release.SetResult();
        executor = new(new(tools), revoke, audit);
        Assert.Equal("source_scope_denied", (await executor.ExecuteAsync(Request(name), Context, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, probe.Reads);
        Assert.Equal("source_scope_denied", audit.Completion!.ErrorCode);
    }

    [Theory]
    [InlineData("source.pr_files")]
    [InlineData("source.pr_comments")]
    [InlineData("source.pr_checks")]
    public async Task SchemaScopeOutputAndDeadlineAreEnforced(string name)
    {
        var probe = new PullRequestInspectionProbe();
        var executor = new ToolExecutor(new(Tools(probe)), new SourceReviewApprovalProbe(() => { }));
        var request = Request(name);
        var token = TestContext.Current.CancellationToken;
        Assert.Equal("invalid_arguments", (await executor.ExecuteAsync(request with { Arguments = JsonSerializer.SerializeToElement(new { number = 7, page = 1 }) }, Context, token)).ErrorCode);
        Assert.Equal("source_scope_denied", (await executor.ExecuteAsync(request, Context with { SourceScope = null }, token)).ErrorCode);
        Assert.Equal("capability_denied", (await executor.ExecuteAsync(request, Context with { ToolsEnabled = false }, token)).ErrorCode);
        var small = await executor.ExecuteAsync(request, Context with { MaximumOutputCharacters = 80 }, token);
        Assert.Equal("source_output_limit", small.ErrorCode);
        Assert.Null(small.Content);
        var success = await executor.ExecuteAsync(request, Context, token);
        Assert.True(success.IsSuccess);
        using var json = JsonDocument.Parse(success.Content!);
        Assert.Equal(7, json.RootElement.GetProperty("Revision").GetProperty("Number").GetInt32());
        probe.OnRead = cancel => Task.Delay(Timeout.Infinite, cancel);
        Assert.Equal("tool_timeout", (await executor.ExecuteAsync(request, Context with { Timeout = TimeSpan.FromMilliseconds(30) }, token)).ErrorCode);
    }

    [Theory]
    [InlineData("source.pr_write")]
    [InlineData("source.pr_files_other")]
    [InlineData("mcp.source.pr_checks")]
    public void SourceToolExpansionIsAnExactReadOnlyAllowlist(string name) => Assert.False(Context.IsToolAllowed(name));
}
