using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class ToolSchemaFixture
{
    private readonly ToolExecutor _executor;
    private readonly ToolExecutionContext _context = new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
        ApprovalPolicy.AskEveryTime);

    public ToolSchemaFixture(string schema)
    {
        Tool = new(schema);
        Audit.Release.TrySetResult();
        _executor = new(new ToolRegistry([Tool]), new PolicyApprovalService((_, _, _) =>
        {
            ApprovalRequests++;
            return ValueTask.FromResult(true);
        }), Audit);
    }

    public StringSchemaProbeTool Tool { get; }
    public ToolProgressAuditSink Audit { get; } = new();
    public int ApprovalRequests { get; private set; }

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken? cancellationToken = null) =>
        ExecuteAsync(JsonSerializer.Deserialize<JsonElement>(arguments), cancellationToken);

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken? cancellationToken = null) =>
        _executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), Tool.Descriptor.Name, arguments),
            _context, cancellationToken ?? TestContext.Current.CancellationToken);

    public void AssertOutcome(ToolResult result, bool accepted, string errorCode = "invalid_arguments")
    {
        Assert.Equal(accepted, result.IsSuccess);
        Assert.Equal(accepted, Tool.WasExecuted);
        Assert.Equal(accepted ? 1 : 0, ApprovalRequests);
        Assert.Equal(result.ErrorCode, Audit.Completion!.ErrorCode);
        Assert.Equal(accepted, Audit.Completion.IsSuccess);
        if (!accepted) Assert.Equal(errorCode, result.ErrorCode);
    }
}
