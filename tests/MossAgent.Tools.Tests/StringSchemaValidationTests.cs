using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class StringSchemaValidationTests
{
    [Theory]
    [InlineData(1, 2, "", false)]
    [InlineData(1, 2, "a", true)]
    [InlineData(1, 2, "ab", true)]
    [InlineData(1, 2, "abc", false)]
    [InlineData(1, 1, "苔", true)]
    [InlineData(1, 1, "🌿", true)]
    [InlineData(2, 2, "🌿", false)]
    [InlineData(1, 1, "🌿🌿", false)]
    [InlineData(2, 2, "e\u0301", true)]
    [InlineData(0, 0, "", true)]
    public async Task StringLengthConstraints_RunBeforeApprovalAndCountUnicodeScalars(
        int minimum, int maximum, string value, bool accepted)
    {
        var schema = JsonSerializer.Serialize(new
        {
            type = "object",
            required = new[] { "text" },
            properties = new { text = new { type = "string", minLength = minimum, maxLength = maximum } }
        });
        var tool = new StringSchemaProbeTool(schema);
        var approvalRequests = 0;
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) =>
        {
            approvalRequests++;
            return ValueTask.FromResult(true);
        }));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            ApprovalPolicy.AskEveryTime);

        var result = await executor.ExecuteAsync(FileEditingFixture.Request(tool, new { text = value }),
            context, TestContext.Current.CancellationToken);

        Assert.Equal(accepted, result.IsSuccess);
        Assert.Equal(accepted, tool.WasExecuted);
        Assert.Equal(accepted ? 1 : 0, approvalRequests);
        if (accepted) Assert.Equal("Validated.", result.Summary);
        else
        {
            Assert.Equal("invalid_arguments", result.ErrorCode);
            if (value.Length > 0) Assert.DoesNotContain(value, result.Summary, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("\"minLength\":-1")]
    [InlineData("\"minLength\":0.5")]
    [InlineData("\"minLength\":\"1\"")]
    [InlineData("\"maxLength\":null")]
    [InlineData("\"maxLength\":-1")]
    [InlineData("\"minLength\":2,\"maxLength\":1")]
    public async Task InvalidLengthConstraints_FailClosedBeforeApproval(string constraints)
    {
        var tool = new StringSchemaProbeTool("{\"type\":\"string\"," + constraints + "}");
        var approvalRequests = 0;
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) =>
        {
            approvalRequests++;
            return ValueTask.FromResult(true);
        }));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            ApprovalPolicy.AskEveryTime);

        var result = await executor.ExecuteAsync(FileEditingFixture.Request(tool, "sensitive marker"),
            context, TestContext.Current.CancellationToken);

        Assert.Equal("tool_schema_invalid", result.ErrorCode);
        Assert.Equal(0, approvalRequests);
        Assert.False(tool.WasExecuted);
        Assert.DoesNotContain("sensitive marker", result.Summary, StringComparison.Ordinal);
    }
}
