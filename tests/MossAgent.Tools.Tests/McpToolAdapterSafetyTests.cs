using System.Net;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpToolAdapterSafetyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemoteErrorContent_IsNotExposedAsAnErrorSummary(bool isError)
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) => Task.FromResult(message.GetProperty("method").GetString() == "tools/call"
                ? McpHttpTestHandler.JsonResponse(new
                {
                    jsonrpc = "2.0",
                    id = message.GetProperty("id").GetInt64(),
                    result = new { content = new[] { new { type = "text", text = "synthetic-tool-secret" } }, isError }
                })
                : McpHttpTestHandler.DefaultResponse(message))
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);
        var adapter = new McpToolAdapter("mcp.fixture.echo", "echo", new McpToolDefinition("echo", "Echo",
            JsonSerializer.SerializeToElement(new { type = "object" }), false), client);
        var context = new ToolExecutionContext(Guid.NewGuid(), Environment.CurrentDirectory, [], ApprovalPolicy.FullAccess);

        var result = await adapter.ExecuteAsync(new ToolRequest("call-1", adapter.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, TestContext.Current.CancellationToken);

        Assert.Equal(!isError, result.IsSuccess);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, adapter.Descriptor.RiskLevel);
        if (isError)
        {
            Assert.Equal("mcp_tool_error", result.ErrorCode);
            Assert.DoesNotContain("synthetic-tool-secret", JsonSerializer.Serialize(result), StringComparison.Ordinal);
        }
        else Assert.Equal("synthetic-tool-secret", result.Content);
    }

    [Theory]
    [InlineData(401, "mcp_authentication_failed", "凭据")]
    [InlineData(404, "mcp_session_invalid", "会话已失效")]
    public async Task ClassifiedTransportFailures_ReturnActionableErrorCodes(
        int status, string expectedError, string expectedText)
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) => Task.FromResult(message.GetProperty("method").GetString() == "tools/call"
                ? new HttpResponseMessage((HttpStatusCode)status)
                : McpHttpTestHandler.DefaultResponse(message))
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);
        var adapter = new McpToolAdapter("mcp.fixture.echo", "echo", new McpToolDefinition("echo", "Echo",
            JsonSerializer.SerializeToElement(new { type = "object" }), false), client);
        var context = new ToolExecutionContext(Guid.NewGuid(), Environment.CurrentDirectory, [], ApprovalPolicy.FullAccess);

        var result = await adapter.ExecuteAsync(new ToolRequest("call-1", adapter.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.ErrorCode);
        Assert.Contains(expectedText, result.Summary, StringComparison.Ordinal);
    }
}
