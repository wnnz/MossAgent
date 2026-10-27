using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Resources;
using MossAgent.Domain;

namespace MossAgent.Tools.BuiltIn.Execution;

public sealed class ToolExecutor(
    ToolRegistry registry,
    IToolApprovalService approvalService,
    IToolExecutionAuditSink? auditSink = null,
    TimeProvider? timeProvider = null,
    MossAgent.Tools.Abstractions.Files.IFileReadHandleValidator? readHandleValidator = null) : IToolExecutor
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Bounded settle for attributing a cancelled pending approval to workspace
    /// revocation when the caller-side cancellation arrived ahead of the lease's own revoke
    /// (change-event subscribers may run before the monitor's revoke handler).</summary>
    private static readonly TimeSpan RevocationAttributionWindow = TimeSpan.FromMilliseconds(50);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var capturedRoots = new CapturedToolRoots(context.AuthorizedRoots);
        context = context with
        {
            AuthorizedRoots = capturedRoots,
            ReadHandleValidator = readHandleValidator ?? context.ReadHandleValidator
        };
        ReportProgress(request, context, ToolExecutionPhase.Validating);
        registry.TryGet(request.ToolName, out var tool);
        var executionId = await StartAuditAsync(
            request, context, tool?.Descriptor.RiskLevel, cancellationToken);
        if (auditSink is not null && executionId is null)
        {
            return ToolOutputLimiter.Limit(ToolResult.Failure(
                "无法写入工具审计记录，已拒绝执行。", "audit_unavailable"),
                context.MaximumOutputCharacters);
        }

        if (tool is null)
        {
            return await CompleteAuditAsync(
                executionId, null,
                ToolResult.Failure($"未知工具：{request.ToolName}", "tool_not_found"), context, request);
        }

        var validationResult = ValidateArguments(tool, request, cancellationToken);
        if (validationResult is not null)
        {
            return await CompleteAuditAsync(executionId, null, validationResult, context, request);
        }

        if (!context.ToolsEnabled || !ToolCapabilityPolicy.IsAllowed(tool.Descriptor, context.ApprovalPolicy))
        {
            var capabilityResult = ToolResult.Failure(
                context.ToolsEnabled ? "当前审批策略不允许此工具所需的能力。" : ToolExecutionText.Get("ProjectlessToolsDenied"), "capability_denied");
            return await CompleteAuditAsync(executionId, false, capabilityResult, context, request);
        }

        if (!context.IsToolAllowed(tool.Descriptor.Name))
        {
            return await CompleteAuditAsync(executionId, false,
                ToolResult.Failure(ToolExecutionText.Get("TaskToolScopeDenied"), "tool_scope_denied"), context, request);
        }

        var preflightResult = await ValidateBeforeApprovalAsync(
            executionId, tool, request, context, cancellationToken);
        if (preflightResult is not null)
        {
            return preflightResult;
        }

        return await ExecuteAuthorizedAsync(executionId, tool, request, context, capturedRoots, cancellationToken);
    }

    private async Task<ToolResult> ExecuteAuthorizedAsync(Guid? executionId, IAgentTool tool, ToolRequest request,
        ToolExecutionContext context, CapturedToolRoots roots, CancellationToken cancellationToken)
    {
        TaskWorkspaceOperation operation;
        try { operation = await TaskWorkspaceOperation.EnterAsync(context, cancellationToken); }
        catch (WorkspaceAuthorizationException)
        { return await CompleteAuditAsync(executionId, false, AuthorizationChanged(null), context, request); }
        catch (OperationCanceledException)
        { return await CompleteAuditAsync(executionId, null, InterruptedResult(true), context, request); }
        catch (Exception)
        { return await CompleteAuditAsync(executionId, false, ToolResult.Failure(ToolExecutionText.Get("WorkspaceAccessDenied"), "workspace_denied"), context, request); }
        await using (operation)
        {
            ReportProgress(request, context, ToolExecutionPhase.AwaitingApproval);
            var approved = await RequestApprovalAsync(tool, request, context, operation.Token);
            ToolResult result;
            if (approved != true)
            {
                if (operation.Token.IsCancellationRequested && !operation.IsRevoked)
                    await operation.AwaitRevocationAsync(RevocationAttributionWindow, _timeProvider).ConfigureAwait(false);
                result = operation.Token.IsCancellationRequested
                    ? ToolResult.Failure(ToolExecutionText.Get("ApprovalCancelled"), "tool_cancelled")
                    : approved == false ? ToolResult.Failure("用户或策略拒绝了工具调用。", "approval_denied")
                    : ToolResult.Failure(ToolExecutionText.Get("ApprovalFailed"), "approval_failed");
            }
            else
            {
                result = await ExecuteToolAsync(tool, request, context, roots, operation.Token);
                if (context.WorkspaceGuard is ITaskWorkspaceLeaseProvider && !operation.IsRevoked)
                {
                    var validation = await ToolWorkspaceValidation.ValidateAsync(context, operation.Token, _timeProvider);
                    if (validation is not null && !operation.IsRevoked)
                        result = validation with { Artifacts = result.Artifacts, FileChanges = result.FileChanges,
                            FileChangesTruncated = result.FileChangesTruncated, Dialogue = result.Dialogue ?? validation.Dialogue };
                }
            }
            if (operation.IsRevoked) result = AuthorizationChanged(result);
            return await CompleteAuditAsync(executionId, approved, result, context, request);
        }
    }

    private static ToolResult AuthorizationChanged(ToolResult? result) => ToolResult.Failure(
        ToolExecutionText.Get("WorkspaceAuthorizationChanged") + (result is null ? "" : " " + result.Summary),
        "workspace_authorization_changed", result?.Dialogue) with
        { Artifacts = result?.Artifacts, FileChanges = result?.FileChanges, FileChangesTruncated = result?.FileChangesTruncated == true };

    private static ToolResult? ValidateArguments(IAgentTool tool, ToolRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ToolInputSchemaValidator.TryValidate(tool.Descriptor.InputSchemaJson, request.Arguments,
                out var error, out var schemaInvalid, cancellationToken)
                ? null : ToolResult.Failure(error, schemaInvalid ? "tool_schema_invalid" : "invalid_arguments");
        }
        catch (OperationCanceledException)
        {
            return InterruptedResult(true);
        }
    }

    private async Task<ToolResult?> ValidateBeforeApprovalAsync(
        Guid? executionId,
        IAgentTool tool,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workspaceResult = await ToolWorkspaceValidation.ValidateAsync(context, cancellationToken, _timeProvider);
        if (workspaceResult is not null)
            return await CompleteAuditAsync(executionId, false, workspaceResult, context, request);

        var asyncResult = await ToolAsyncRequestValidation.ValidateAsync(tool, request, context, cancellationToken, _timeProvider);
        if (asyncResult is not null)
            return await CompleteAuditAsync(executionId, false, asyncResult, context, request);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tool is IToolRequestPrevalidator prevalidator) prevalidator.ValidateRequest(request, context);
            return null;
        }
        catch (OperationCanceledException)
        {
            return await CompleteAuditAsync(executionId, null,
                ToolResult.Failure("工具请求已取消。", "tool_cancelled"), context, request);
        }
        catch (UnauthorizedAccessException exception)
        {
            return await CompleteAuditAsync(executionId, false,
                ToolResult.Failure(exception.Message, "path_denied"), context, request);
        }
        catch (Exception)
        {
            return await CompleteAuditAsync(executionId, false,
                ToolResult.Failure("工具请求路径无效。", "invalid_arguments"), context, request);
        }
    }

    private async Task<ToolResult> ExecuteToolAsync(
        IAgentTool tool,
        ToolRequest request,
        ToolExecutionContext context,
        CapturedToolRoots capturedRoots,
        CancellationToken cancellationToken)
    {
        var duration = context.Timeout ?? TimeSpan.FromMinutes(2);
        var started = _timeProvider.GetTimestamp();
        using var deadline = new CancellationTokenSource(duration, _timeProvider);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            capturedRoots.Validate();
            var workspaceResult = await ToolWorkspaceValidation.ValidateAsync(context, timeout.Token, _timeProvider);
            if (timeout.IsCancellationRequested) return InterruptedResult(cancellationToken.IsCancellationRequested);
            if (workspaceResult is not null) return workspaceResult;
            if (tool is IToolRequestAsyncPrevalidator)
            {
                var asyncResult = await ToolAsyncRequestValidation.ValidateAsync(tool, request, context, timeout.Token, _timeProvider);
                if (timeout.IsCancellationRequested || HasDeadlineElapsed(started, duration)) return InterruptedResult(cancellationToken.IsCancellationRequested);
                if (asyncResult is not null) return asyncResult;
            }
            ReportProgress(request, context, ToolExecutionPhase.Executing);
            timeout.Token.ThrowIfCancellationRequested();
            if (HasDeadlineElapsed(started, duration)) return InterruptedResult(false);
            capturedRoots.Validate();
            var result = await tool.ExecuteAsync(request, context, timeout.Token);
            return timeout.IsCancellationRequested || HasDeadlineElapsed(started, duration)
                ? InterruptedResult(cancellationToken.IsCancellationRequested, result.Dialogue, result.Summary) with
                {
                    Artifacts = result.Artifacts,
                    FileChanges = result.FileChanges,
                    FileChangesTruncated = result.FileChangesTruncated
                }
                : result;
        }
        catch (OperationCanceledException exception)
        {
            var interrupted = exception as ToolRunInterruptedException;
            return InterruptedResult(cancellationToken.IsCancellationRequested, interrupted?.Dialogue) with
            {
                FileChanges = interrupted?.FileChanges,
                FileChangesTruncated = interrupted?.FileChangesTruncated == true
            };
        }
        catch (UnauthorizedAccessException exception)
        {
            return ToolResult.Failure(exception.Message, "path_denied");
        }
        catch (FileEditException exception)
        {
            return ToolResult.Failure(exception.Message, exception.ErrorCode);
        }
        catch (MossAgent.Tools.Abstractions.Files.FileMutationSessionException exception)
        {
            return timeout.IsCancellationRequested || HasDeadlineElapsed(started, duration)
                ? InterruptedResult(cancellationToken.IsCancellationRequested,
                    completedSummary: exception.HasSideEffects ? exception.Message : null)
                : ToolResult.Failure(exception.Message, exception.ErrorCode);
        }
        catch (MossAgent.Tools.Abstractions.Browser.BrowserTakeoverException exception)
        {
            return ToolResult.Failure(exception.Message, exception.ErrorCode);
        }
        catch (MossAgent.Tools.Abstractions.Browser.BrowserProfileAccessDeniedException exception)
        {
            return ToolResult.Failure(exception.Message, exception.ErrorCode);
        }
        catch (Exception)
        {
            return ToolResult.Failure("工具执行失败，请检查工具配置和运行环境。", "tool_failed");
        }
    }

    private bool HasDeadlineElapsed(long started, TimeSpan duration) =>
        duration != Timeout.InfiniteTimeSpan && _timeProvider.GetElapsedTime(started) >= duration;

    private static ToolResult InterruptedResult(bool cancelled, IReadOnlyList<ToolDialogueMessage>? dialogue = null,
        string? completedSummary = null) =>
        ToolResult.Failure(ToolExecutionText.Get(cancelled ? "ExecutionCancelled" : "ExecutionTimedOut")
            + (completedSummary is null ? "" : " " + completedSummary),
            cancelled ? "tool_cancelled" : "tool_timeout", dialogue);

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
        ToolResult result,
        ToolExecutionContext context,
        ToolRequest request)
    {
        ReportProgress(request, context, ToolExecutionPhase.Finalizing);
        var limited = ToolOutputLimiter.Limit(result, context.MaximumOutputCharacters);
        if (auditSink is null || executionId is null)
        {
            return limited;
        }

        try
        {
            await auditSink.CompleteAsync(
                executionId.Value,
                new ToolExecutionAuditCompletion(
                    DateTimeOffset.UtcNow, approved, result.IsSuccess, result.ErrorCode,
                    limited.OutputCharacterCount),
                limited.Artifacts ?? [],
                CancellationToken.None);
            return limited;
        }
        catch
        {
            return ToolOutputLimiter.Limit(limited with { Summary = limited.Summary + "（审计完成状态写入失败。）" },
                context.MaximumOutputCharacters);
        }
    }

    private static void ReportProgress(ToolRequest request, ToolExecutionContext context, ToolExecutionPhase phase)
    {
        try { context.Progress?.Invoke(new ToolExecutionProgress(context.TaskId, request.CallId, request.ToolName, phase)); }
        catch (Exception) { }
    }

}
