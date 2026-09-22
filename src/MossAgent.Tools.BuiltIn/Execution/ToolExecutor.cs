using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Execution;

public sealed class ToolExecutor(
    ToolRegistry registry,
    IToolApprovalService approvalService,
    IToolExecutionAuditSink? auditSink = null) : IToolExecutor
{
    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        registry.TryGet(request.ToolName, out var tool);
        var executionId = await StartAuditAsync(
            request, context, tool?.Descriptor.RiskLevel, cancellationToken);
        if (auditSink is not null && executionId is null)
        {
            return ToolResult.Failure(
                "无法写入工具审计记录，已拒绝执行。", "audit_unavailable");
        }

        if (tool is null)
        {
            return await CompleteAuditAsync(
                executionId, null,
                ToolResult.Failure($"未知工具：{request.ToolName}", "tool_not_found"));
        }

        if (!ToolInputSchemaValidator.TryValidate(
                tool.Descriptor.InputSchemaJson,
                request.Arguments,
                out var validationError,
                out var schemaInvalid))
        {
            var validationResult = ToolResult.Failure(
                validationError,
                schemaInvalid ? "tool_schema_invalid" : "invalid_arguments");
            return await CompleteAuditAsync(executionId, null, validationResult);
        }

        var approved = await RequestApprovalAsync(
            tool, request, context, cancellationToken);
        if (approved != true)
        {
            var approvalResult = approved == false
                ? ToolResult.Failure("用户或策略拒绝了工具调用。", "approval_denied")
                : ToolResult.Failure("工具审批已取消或失败。", "approval_failed");
            return await CompleteAuditAsync(executionId, approved, approvalResult);
        }

        var result = await ExecuteToolAsync(tool, request, context, cancellationToken);
        return await CompleteAuditAsync(executionId, true, result);
    }

    private static async Task<ToolResult> ExecuteToolAsync(
        IAgentTool tool,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(context.Timeout ?? TimeSpan.FromMinutes(2));
        try
        {
            var result = await tool.ExecuteAsync(request, context, timeout.Token);
            return LimitOutput(result, context.MaximumOutputCharacters);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ToolResult.Failure("工具执行超时。", "tool_timeout");
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Failure("工具执行已取消。", "tool_cancelled");
        }
        catch (Exception exception)
        {
            return ToolResult.Failure(exception.Message, "tool_failed");
        }
    }

    private async Task<bool?> RequestApprovalAsync(
        IAgentTool tool,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await approvalService.IsApprovedAsync(
                tool.Descriptor, request, context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Guid?> StartAuditAsync(
        ToolRequest request,
        ToolExecutionContext context,
        ToolRiskLevel? riskLevel,
        CancellationToken cancellationToken)
    {
        if (auditSink is null)
        {
            return null;
        }

        try
        {
            return await auditSink.StartAsync(
                new ToolExecutionAuditStart(
                    context.TaskId, request.CallId, request.ToolName, riskLevel,
                    context.ApprovalPolicy, DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ToolResult> CompleteAuditAsync(
        Guid? executionId,
        bool? approved,
        ToolResult result)
    {
        if (auditSink is null || executionId is null)
        {
            return result;
        }

        try
        {
            await auditSink.CompleteAsync(
                executionId.Value,
                new ToolExecutionAuditCompletion(
                    DateTimeOffset.UtcNow, approved, result.IsSuccess, result.ErrorCode,
                    result.Content?.Length ?? 0),
                result.Artifacts ?? [],
                CancellationToken.None);
            return result;
        }
        catch
        {
            return result with { Summary = result.Summary + "（审计完成状态写入失败。）" };
        }
    }

    private static ToolResult LimitOutput(ToolResult result, int maximumCharacters)
    {
        if (result.Content is null || result.Content.Length <= maximumCharacters)
        {
            return result;
        }

        var suffix = $"\n\n[输出已截断，共 {result.Content.Length} 个字符]";
        var available = Math.Max(0, maximumCharacters - suffix.Length);
        return result with { Content = result.Content[..available] + suffix };
    }
}
