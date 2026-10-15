using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpSchemaPipelineTests
{
    [Theory]
    [InlineData(ApprovalPolicy.FullAccess, true)]
    [InlineData(ApprovalPolicy.FullAccess, false)]
    [InlineData(ApprovalPolicy.AskEveryTime, true)]
    [InlineData(ApprovalPolicy.AskEveryTime, false)]
    public async Task RemoteCall_IsSentOnlyAfterFullSchemaValidationAndApproval(ApprovalPolicy policy, bool accepted)
    {
        var handler = new McpHttpTestHandler();
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);
        var schema = JsonSerializer.Deserialize<JsonElement>("""
            {"type":"object","required":["input"],"properties":{"input":{"$ref":"#/$defs/input"}},
             "$defs":{"input":{"type":"object","properties":{"name":{"type":"string"}},"additionalProperties":false}},
             "additionalProperties":false}
            """);
        var adapter = new McpToolAdapter("mcp.fixture.schema", "schema", new("schema", "Schema fixture", schema, false), client);
        var approvals = 0;
        var audit = new ToolProgressAuditSink();
        audit.Release.TrySetResult();
        var executor = new ToolExecutor(new ToolRegistry([adapter]), new PolicyApprovalService((_, _, _) =>
        {
            approvals++;
            return ValueTask.FromResult(true);
        }), audit);
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [], policy);
        var arguments = JsonSerializer.Deserialize<JsonElement>(accepted
            ? """{"input":{"name":"safe"}}""" : """{"input":{"name":"safe","extra":"synthetic-secret"}}""");
        var result = await executor.ExecuteAsync(new("mcp-call", adapter.Descriptor.Name, arguments), context,
            TestContext.Current.CancellationToken);

        Assert.Equal(accepted, result.IsSuccess);
        Assert.Equal(accepted ? 1 : 0, handler.Requests.Count(request => request.GetProperty("method").GetString() == "tools/call"));
        Assert.Equal(policy == ApprovalPolicy.AskEveryTime && accepted ? 1 : 0, approvals);
        Assert.Equal(result.ErrorCode, audit.Completion!.ErrorCode);
        if (!accepted) Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.DoesNotContain("synthetic-secret", result.Summary, StringComparison.Ordinal);
    }
}
