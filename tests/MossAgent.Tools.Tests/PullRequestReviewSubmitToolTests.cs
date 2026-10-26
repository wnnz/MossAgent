using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.SourceControl;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class PullRequestReviewSubmitToolTests
{
    private static ToolExecutionContext Context(bool canWrite = true, ApprovalPolicy policy = ApprovalPolicy.FullAccess,
        PullRequestRevision? bound = null) => new(Guid.NewGuid(), string.Empty, [], policy)
    { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo", canWrite), SourceReviewRevision = bound };

    private static ToolRequest Request(object? overrides = null)
    {
        var arguments = new Dictionary<string, object>
        {
            ["number"] = 7, ["base_revision"] = new string('a', 40),
            ["head_revision"] = new string('b', 40), ["decision"] = "approve", ["body"] = "looks good"
        };
        if (overrides is not null)
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(overrides))!) arguments[pair.Key] = pair.Value;
        return new("write-call", "source.pr_review_submit", JsonSerializer.SerializeToElement(arguments));
    }

    [Fact]
    public void DescriptorIsAnExternalWriteHiddenFromTheModel()
    {
        var tool = new PullRequestReviewSubmitTool(new PullRequestWriterProbe());
        Assert.Equal("source.pr_review_submit", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.Network, tool.Descriptor.RequiredCapabilities);
        Assert.False(tool.Descriptor.IsModelVisible);
        Assert.Empty(new ToolRegistry([tool]).Descriptors);
    }

    [Theory]
    [InlineData("{\"number\":0}", "zero number")]
    [InlineData("{\"decision\":\"lgtm\"}", "unknown decision")]
    [InlineData("{\"decision\":1}", "non-string decision")]
    [InlineData("{\"base_revision\":\"short\"}", "short base")]
    [InlineData("{\"head_revision\":\"zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz\"}", "non-hex head")]
    [InlineData("{\"extra\":1}", "extra property")]
    public async Task SchemaRejectsMalformedRequestsBeforeWriting(string patch, string scenario)
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.SerializeToElement(new
        {
            number = 7, base_revision = new string('a', 40), head_revision = new string('b', 40),
            decision = "approve", body = "looks good"
        }))!;
        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patch)!) arguments[pair.Key] = pair.Value;
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_review_submit",
            JsonSerializer.SerializeToElement(arguments)), Context(), TestContext.Current.CancellationToken);
        Assert.True("invalid_arguments" == result.ErrorCode, scenario);
        Assert.Equal(0, writer.Reviews);
    }

    [Theory]
    [InlineData("number")]
    [InlineData("base_revision")]
    [InlineData("head_revision")]
    [InlineData("decision")]
    public async Task RequiredFieldsAreEnforced(string missing)
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.SerializeToElement(new
        {
            number = 7, base_revision = new string('a', 40), head_revision = new string('b', 40),
            decision = "approve", body = "looks good"
        }))!;
        arguments.Remove(missing);
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_review_submit",
            JsonSerializer.SerializeToElement(arguments)), Context(), TestContext.Current.CancellationToken);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, writer.Reviews);
    }

    [Fact]
    public async Task BodyIsOptionalAndOversizedBodyIsRejectedByTheSchema()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.SerializeToElement(new
        {
            number = 7, base_revision = new string('a', 40), head_revision = new string('b', 40), decision = "approve"
        }))!;
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_review_submit",
            JsonSerializer.SerializeToElement(arguments)), Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.False(writer.BodyWasSent);
        Assert.Null(writer.LastBody);
        var oversized = await executor.ExecuteAsync(Request(new { body = new string('x', 4097) }), Context(),
            TestContext.Current.CancellationToken);
        Assert.Equal("invalid_arguments", oversized.ErrorCode);
        Assert.Equal(1, writer.Reviews);
    }

    [Fact]
    public async Task ReadOnlyScopeIsDeniedBeforeThePortRuns()
    {
        var writer = new PullRequestWriterProbe();
        var approval = new SourceReviewApprovalProbe(() => Assert.Fail("No approval expected."));
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), approval);
        var result = await executor.ExecuteAsync(Request(), Context(canWrite: false), TestContext.Current.CancellationToken);
        Assert.Equal("tool_scope_denied", result.ErrorCode);
        Assert.Equal(0, writer.Validations);
        Assert.Equal(0, writer.Reviews);
    }

    [Fact]
    public async Task MissingScopeIsDeniedDuringPrevalidation()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context() with { SourceScope = null }, TestContext.Current.CancellationToken);
        Assert.Equal("source_scope_denied", result.ErrorCode);
        Assert.Equal(0, writer.Reviews);
    }

    [Fact]
    public async Task WriteScopeRunsWritePrevalidationAndReturnsTheReceipt()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, writer.Validations);
        Assert.Equal(1, writer.Reviews);
        Assert.Equal(PullRequestReviewDecision.Approve, writer.LastDecision);
        Assert.Equal("looks good", writer.LastBody);
        Assert.Equal(31, JsonDocument.Parse(result.Content!).RootElement.GetProperty("ReviewId").GetInt64());
        Assert.False(JsonDocument.Parse(result.Content!).RootElement.GetProperty("Reconciled").GetBoolean());
    }

    [Fact]
    public async Task ReconciledReceiptReportsTheCaveatedSummary()
    {
        var writer = new PullRequestWriterProbe { ReviewReceipt = new(45, true) };
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.True(JsonDocument.Parse(result.Content!).RootElement.GetProperty("Reconciled").GetBoolean());
        Assert.Contains("未知", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovalDenialAndRevocationDuringApprovalPreventTheWrite()
    {
        var writer = new PullRequestWriterProbe();
        var denied = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]),
            new SourceReviewApprovalProbe(() => { }, false));
        Assert.Equal("approval_denied", (await denied.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(1, writer.Validations);
        Assert.Equal(0, writer.Reviews);

        var revoking = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]),
            new SourceReviewApprovalProbe(() => writer.IsValid = false));
        Assert.Equal("source_scope_denied", (await revoking.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, writer.Reviews);
    }

    [Theory]
    [InlineData("source_write_rejected")]
    [InlineData("source_write_unconfirmed")]
    [InlineData("source_write_unknown")]
    [InlineData("source_revision_changed")]
    [InlineData("source_scope_denied")]
    public async Task WriterFailuresMapToStableResultCodes(string code)
    {
        var writer = new PullRequestWriterProbe { ReviewFailureCode = code };
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(1, writer.Reviews);
    }

    [Fact]
    public async Task BoundReviewChatsAndReadOnlyPoliciesNeverReachTheWriter()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var bound = Context(bound: new(7, new string('a', 40), new string('b', 40)));
        Assert.Equal("tool_scope_denied", (await executor.ExecuteAsync(Request(), bound, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal("capability_denied", (await executor.ExecuteAsync(Request(), Context(policy: ApprovalPolicy.ReadOnly),
            TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, writer.Reviews);
    }

    [Fact]
    public async Task TimeoutInterruptsWithoutRetry()
    {
        var writer = new PullRequestWriterProbe { OnReview = token => Task.Delay(Timeout.Infinite, token) };
        var executor = new ToolExecutor(new([new PullRequestReviewSubmitTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context() with { Timeout = TimeSpan.FromMilliseconds(30) },
            TestContext.Current.CancellationToken);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(1, writer.Reviews);
    }
}
