using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.SourceControl;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class PullRequestCreateToolTests
{
    private static ToolExecutionContext Context(bool canWrite = true, ApprovalPolicy policy = ApprovalPolicy.FullAccess,
        PullRequestRevision? bound = null) => new(Guid.NewGuid(), string.Empty, [], policy)
    { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo", canWrite), SourceReviewRevision = bound };

    private static ToolRequest Request(object? overrides = null)
    {
        var arguments = new Dictionary<string, object>
        {
            ["title"] = "Add feature", ["head"] = "feature", ["base"] = "main",
            ["body"] = "notes", ["draft"] = true
        };
        if (overrides is not null)
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(overrides))!) arguments[pair.Key] = pair.Value;
        return new("write-call", "source.pr_create", JsonSerializer.SerializeToElement(arguments));
    }

    [Fact]
    public void DescriptorIsAnExternalWriteHiddenFromTheModel()
    {
        var tool = new PullRequestCreateTool(new PullRequestWriterProbe());
        Assert.Equal("source.pr_create", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.Network, tool.Descriptor.RequiredCapabilities);
        Assert.False(tool.Descriptor.IsModelVisible);
        Assert.Empty(new ToolRegistry([tool]).Descriptors);
    }

    [Fact]
    public void SchemaDeclaresCreateFieldsWithoutAnyRevision()
    {
        var tool = new PullRequestCreateTool(new PullRequestWriterProbe());
        using var schema = JsonDocument.Parse(tool.Descriptor.InputSchemaJson);
        var properties = schema.RootElement.GetProperty("properties");
        Assert.Equal(new[] { "base", "body", "draft", "head", "title" },
            properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "base", "head", "title" },
            schema.RootElement.GetProperty("required").EnumerateArray()
                .Select(item => item.GetString()!).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("boolean", properties.GetProperty("draft").GetProperty("type").GetString());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
    }

    [Theory]
    [InlineData("{\"title\":\"\"}", "empty title")]
    [InlineData("{\"head\":\"\"}", "empty head")]
    [InlineData("{\"extra\":1}", "extra property")]
    [InlineData("{\"draft\":\"yes\"}", "non-boolean draft")]
    [InlineData("{\"number\":7}", "revision field leaks")]
    public async Task SchemaRejectsMalformedRequestsBeforeCreating(string patch, string scenario)
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.SerializeToElement(new
        {
            title = "Add feature", head = "feature", @base = "main"
        }))!;
        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patch)!) arguments[pair.Key] = pair.Value;
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_create",
            JsonSerializer.SerializeToElement(arguments)), Context(), TestContext.Current.CancellationToken);
        Assert.True("invalid_arguments" == result.ErrorCode, scenario);
        Assert.Equal(0, writer.Creates);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("head")]
    [InlineData("base")]
    public async Task RequiredFieldsAreEnforced(string missing)
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.SerializeToElement(new
        {
            title = "Add feature", head = "feature", @base = "main"
        }))!;
        arguments.Remove(missing);
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_create",
            JsonSerializer.SerializeToElement(arguments)), Context(), TestContext.Current.CancellationToken);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, writer.Creates);
    }

    [Fact]
    public void IsToolAllowedGatesOnTheWriteGrant()
    {
        Assert.True(Context().IsToolAllowed("source.pr_create"));
        Assert.False(Context(canWrite: false).IsToolAllowed("source.pr_create"));
    }

    [Fact]
    public async Task ReadOnlyScopeIsDeniedBeforeThePortRuns()
    {
        var writer = new PullRequestWriterProbe();
        var approval = new SourceReviewApprovalProbe(() => Assert.Fail("No approval expected."));
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), approval);
        var result = await executor.ExecuteAsync(Request(), Context(canWrite: false), TestContext.Current.CancellationToken);
        Assert.Equal("tool_scope_denied", result.ErrorCode);
        Assert.Equal(0, writer.BareValidations);
        Assert.Equal(0, writer.Creates);
    }

    [Fact]
    public async Task MissingScopeIsDeniedDuringPrevalidation()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context() with { SourceScope = null }, TestContext.Current.CancellationToken);
        Assert.Equal("source_scope_denied", result.ErrorCode);
        Assert.Equal(0, writer.Creates);
    }

    [Fact]
    public async Task WriteScopeRunsBareWritePrevalidationAndReturnsTheReceipt()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, writer.BareValidations);
        Assert.Equal(0, writer.Validations);
        Assert.Equal(1, writer.Creates);
        Assert.Equal("Add feature", writer.LastTitle);
        Assert.Equal("feature", writer.LastHead);
        Assert.Equal("main", writer.LastBase);
        Assert.Equal("notes", writer.LastCreateBody);
        Assert.Equal(true, writer.LastDraft);
        var content = JsonDocument.Parse(result.Content!).RootElement;
        Assert.Equal(9, content.GetProperty("Number").GetInt32());
        Assert.Equal(new string('b', 40), content.GetProperty("HeadRevision").GetString());
        Assert.Equal(new string('a', 40), content.GetProperty("BaseRevision").GetString());
        Assert.False(content.GetProperty("Reconciled").GetBoolean());
    }

    [Fact]
    public async Task DraftAndBodyAreOptional()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(new("write-call", "source.pr_create",
            JsonSerializer.SerializeToElement(new { title = "T", head = "h", @base = "b" })),
            Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.False(writer.LastDraft);
        Assert.Null(writer.LastCreateBody);
    }

    [Fact]
    public async Task ReconciledReceiptReportsTheCaveatedSummary()
    {
        var writer = new PullRequestWriterProbe { CreateReceipt = new(9, new string('b', 40), new string('a', 40), true) };
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.True(JsonDocument.Parse(result.Content!).RootElement.GetProperty("Reconciled").GetBoolean());
        Assert.Contains("未知", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovalDenialAndRevocationDuringApprovalPreventTheWrite()
    {
        var writer = new PullRequestWriterProbe();
        var denied = new ToolExecutor(new([new PullRequestCreateTool(writer)]),
            new SourceReviewApprovalProbe(() => { }, false));
        Assert.Equal("approval_denied", (await denied.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(1, writer.BareValidations);
        Assert.Equal(0, writer.Creates);

        var revoking = new ToolExecutor(new([new PullRequestCreateTool(writer)]),
            new SourceReviewApprovalProbe(() => writer.IsValid = false));
        Assert.Equal("source_scope_denied", (await revoking.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, writer.Creates);
    }

    [Theory]
    [InlineData("source_write_rejected")]
    [InlineData("source_write_unconfirmed")]
    [InlineData("source_write_unknown")]
    [InlineData("source_scope_denied")]
    public async Task WriterFailuresMapToStableResultCodes(string code)
    {
        var writer = new PullRequestWriterProbe { CreateFailureCode = code };
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context(), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(1, writer.Creates);
    }

    [Fact]
    public async Task BoundReviewChatsAndReadOnlyPoliciesNeverReachTheWriter()
    {
        var writer = new PullRequestWriterProbe();
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var bound = Context(bound: new(7, new string('a', 40), new string('b', 40)));
        Assert.Equal("tool_scope_denied", (await executor.ExecuteAsync(Request(), bound, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal("capability_denied", (await executor.ExecuteAsync(Request(), Context(policy: ApprovalPolicy.ReadOnly),
            TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal(0, writer.Creates);
    }

    [Fact]
    public async Task TimeoutInterruptsWithoutRetry()
    {
        var writer = new PullRequestWriterProbe { OnCreate = token => Task.Delay(Timeout.Infinite, token) };
        var executor = new ToolExecutor(new([new PullRequestCreateTool(writer)]), new SourceReviewApprovalProbe(() => { }));
        var result = await executor.ExecuteAsync(Request(), Context() with { Timeout = TimeSpan.FromMilliseconds(30) },
            TestContext.Current.CancellationToken);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(1, writer.Creates);
    }
}
