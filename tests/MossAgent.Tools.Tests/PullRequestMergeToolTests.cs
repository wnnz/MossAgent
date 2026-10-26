using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.SourceControl;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class PullRequestMergeToolTests
{
    private static ToolExecutionContext Context(bool canWrite = true, ApprovalPolicy policy = ApprovalPolicy.FullAccess,
        PullRequestRevision? bound = null) => new(Guid.NewGuid(), string.Empty, [], policy)
    { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo", canWrite), SourceReviewRevision = bound };

    private static ToolRequest Request(object? overrides = null)
    {
        var arguments = new Dictionary<string, object>
        {
            ["number"] = 7, ["base_revision"] = new string('a', 40),
            ["head_revision"] = new string('b', 40), ["method"] = "squash"
        };
        if (overrides is not null)
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(overrides))!) arguments[pair.Key] = pair.Value;
        return new("write-call", "source.pr_merge", JsonSerializer.SerializeToElement(arguments));
    }

    [Fact]
    public void DescriptorIsAnExternalWriteHiddenFromTheModel()
    {
        var tool = new PullRequestMergeTool(new PullRequestWriterProbe());
        Assert.Equal("source.pr_merge", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.Network, tool.Descriptor.RequiredCapabilities);
        Assert.False(tool.Descriptor.IsModelVisible);
        Assert.Empty(new ToolRegistry([tool]).Descriptors);
    }

    [Fact]
    public void SchemaDeclaresTheThreeRevisionFieldsPlusTheMethodEnum()
    {
        var tool = new PullRequestMergeTool(new PullRequestWriterProbe());
        using var schema = JsonDocument.Parse(tool.Descriptor.InputSchemaJson);
        var properties = schema.RootElement.GetProperty("properties");
        Assert.Equal(new[] { "base_revision", "head_revision", "method", "number" },
            properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "base_revision", "head_revision", "method", "number" },
            schema.RootElement.GetProperty("required").EnumerateArray()
                .Select(item => item.GetString()!).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "merge", "rebase", "squash" },
            properties.GetProperty("method").GetProperty("enum").EnumerateArray()
                .Select(item => item.GetString()!).Order(StringComparer.Ordinal).ToArray());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
    }

    [Theory]
    [InlineData("{\"number\":0}", "zero number")]
    [InlineData("{\"method\":\"fast-forward\"}", "unknown method")]
    [InlineData("{\"method\":1}", "non-string method")]
    [InlineData("{\"base_revision\":\"short\"}", "short base")]
    [InlineData("{\"head_revision\":\"zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz\"}", "non-hex head")]
    [InlineData("{\"extra\":1}", "extra property")]
    public async Task SchemaRejectsMalformedRequestsBeforeWriting(string patch, string scenario)
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.SerializeToElement(new
        {
            number = 7, base_revision = new string('a', 40), head_revision = new string('b', 40), method = "merge"
        }))!;
        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patch)!) arguments[pair.Key] = pair.Value;
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_merge",
            JsonSerializer.SerializeToElement(arguments)), Context(), TestContext.Current.CancellationToken);
        Assert.True("invalid_arguments" == result.ErrorCode, scenario);
        Assert.Equal(0, writer.Merges);
    }

    [Theory]
    [InlineData("number")]
    [InlineData("base_revision")]
    [InlineData("head_revision")]
    [InlineData("method")]
    public async Task RequiredFieldsAreEnforced(string missing)
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.SerializeToElement(new
        {
            number = 7, base_revision = new string('a', 40), head_revision = new string('b', 40), method = "merge"
        }))!;
        arguments.Remove(missing);
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_merge",
            JsonSerializer.SerializeToElement(arguments)), Context(), TestContext.Current.CancellationToken);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, writer.Merges);
    }

    [Fact]
    public void IsToolAllowedGatesOnTheWriteGrant()
    {
        Assert.True(Context().IsToolAllowed("source.pr_merge"));
        Assert.False(Context(canWrite: false).IsToolAllowed("source.pr_merge"));
    }

    [Fact]
    public async Task ReadOnlyScopeIsDeniedBeforeThePortRuns()
    {
        var writer = new PullRequestWriterProbe();
        var approval = new SourceReviewApprovalProbe(() => Assert.Fail("No approval expected."));
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), approval);
        var result = await executor.ExecuteAsync(Request(), Context(canWrite: false), TestContext.Current.CancellationToken);
        Assert.Equal("tool_scope_denied", result.ErrorCode);
        Assert.Equal(0, writer.Validations);
        Assert.Equal(0, writer.Merges);
    }

    [Fact]
    public async Task MissingScopeIsDeniedDuringPrevalidation()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context() with { SourceScope = null }, TestContext.Current.CancellationToken);
        Assert.Equal("source_scope_denied", result.ErrorCode);
        Assert.Equal(0, writer.Merges);
    }

    [Fact]
    public async Task WriteScopeRunsWritePrevalidationAndReturnsTheReceipt()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, writer.Validations);
        Assert.Equal(1, writer.Merges);
        Assert.Equal(PullRequestMergeMethod.Squash, writer.LastMethod);
        var content = JsonDocument.Parse(result.Content!).RootElement;
        Assert.Equal(new string('c', 40), content.GetProperty("MergeSha").GetString());
        Assert.False(content.GetProperty("Reconciled").GetBoolean());
    }

    [Fact]
    public async Task ReconciledReceiptReportsTheCaveatedSummary()
    {
        var writer = new PullRequestWriterProbe { MergeReceipt = new(new string('d', 40), true) };
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.True(JsonDocument.Parse(result.Content!).RootElement.GetProperty("Reconciled").GetBoolean());
        Assert.Contains("未知", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovalDenialAndRevocationDuringApprovalPreventTheWrite()
    {
        var writer = new PullRequestWriterProbe();
        var denied = new ToolExecutor(new([new PullRequestMergeTool(writer)]),
            new SourceReviewApprovalProbe(() => { }, false));
        Assert.Equal("approval_denied", (await denied.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(1, writer.Validations);
        Assert.Equal(0, writer.Merges);

        var revoking = new ToolExecutor(new([new PullRequestMergeTool(writer)]),
            new SourceReviewApprovalProbe(() => writer.IsValid = false));
        Assert.Equal("source_scope_denied", (await revoking.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, writer.Merges);
    }

    [Theory]
    [InlineData("source_write_rejected")]
    [InlineData("source_write_unconfirmed")]
    [InlineData("source_write_unknown")]
    [InlineData("source_revision_changed")]
    [InlineData("source_scope_denied")]
    public async Task WriterFailuresMapToStableResultCodes(string code)
    {
        var writer = new PullRequestWriterProbe { MergeFailureCode = code };
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(1, writer.Merges);
    }

    [Fact]
    public async Task BoundReviewChatsAndReadOnlyPoliciesNeverReachTheWriter()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var bound = Context(bound: new(7, new string('a', 40), new string('b', 40)));
        Assert.Equal("tool_scope_denied", (await executor.ExecuteAsync(Request(), bound, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal("capability_denied", (await executor.ExecuteAsync(Request(), Context(policy: ApprovalPolicy.ReadOnly),
            TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, writer.Merges);
    }

    [Fact]
    public async Task TimeoutInterruptsWithoutRetry()
    {
        var writer = new PullRequestWriterProbe { OnMerge = token => Task.Delay(Timeout.Infinite, token) };
        var executor = new ToolExecutor(new([new PullRequestMergeTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context() with { Timeout = TimeSpan.FromMilliseconds(30) },
            TestContext.Current.CancellationToken);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(1, writer.Merges);
    }
}
