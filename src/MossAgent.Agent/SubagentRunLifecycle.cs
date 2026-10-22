using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

internal sealed class SubagentRunLifecycle(
    ISubagentRunRepository? repository,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<SubagentRun> StartAsync(SubagentProfile profile, AiProvider provider,
        ModelProfile model, ToolExecutionContext context, string parentCallId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var run = new SubagentRun(
            Guid.NewGuid(), context.TaskId,
            context.RunId == Guid.Empty ? Guid.NewGuid() : context.RunId,
            parentCallId, profile.Id, profile.Name, profile.TriggerCondition, profile.SystemPrompt,
            provider.Id, provider.Name, provider.Protocol, model.Id, model.ModelId,
            model.ContextLength, model.MaximumOutputTokens, profile.ReasoningEffort,
            profile.ShareContext, repository?.InstanceId ?? Guid.NewGuid().ToString("N"),
            SubagentRunStatus.Running, null, null, now, now, null);
        return repository is null ? run : await repository.StartAsync(run, cancellationToken);
    }

    public async Task<ToolResult> FinishAsync(SubagentRun run, ToolResult result,
        CancellationToken cancellationToken)
    {
        var local = Terminal(run, result.IsSuccess ? SubagentRunStatus.Completed : SubagentRunStatus.Failed,
            result.Summary, result.ErrorCode);
        if (repository is null) return result with { SubagentRun = local };
        try
        {
            var terminal = result.IsSuccess
                ? await repository.CompleteAsync(run.TaskId, run.Id, result.Summary, cancellationToken)
                : await repository.FailAsync(run.TaskId, run.Id, result.Summary,
                    result.ErrorCode ?? "subagent_run_failed", cancellationToken);
            return result with { SubagentRun = terminal };
        }
        catch (Exception)
        {
            return result with
            {
                IsSuccess = false,
                Summary = result.Summary + " 运行已结束，但状态记录保存失败；请核对子代理对话和工具结果。",
                ErrorCode = "subagent_run_record_failed",
                SubagentRun = local with
                {
                    Status = SubagentRunStatus.Failed,
                    ErrorCode = "subagent_run_record_failed",
                    Summary = "子代理状态记录保存失败。"
                }
            };
        }
    }

    public async Task<SubagentRun> CancelAsync(SubagentRun run)
    {
        var local = Terminal(run, SubagentRunStatus.Cancelled,
            "父任务已取消，子代理停止等待；不自动重试。", "tool_cancelled");
        if (repository is null) return local;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3), _timeProvider);
        try
        {
            return await repository.CancelAsync(run.TaskId, run.Id, local.Summary!,
                timeout.Token).WaitAsync(timeout.Token);
        }
        catch (Exception)
        {
            return local;
        }
    }

    public SubagentRun StartFailure(SubagentProfile profile, AiProvider provider, ModelProfile model,
        ToolExecutionContext context, string parentCallId, string errorCode)
    {
        var now = _timeProvider.GetUtcNow();
        return new SubagentRun(
            Guid.NewGuid(), context.TaskId,
            context.RunId == Guid.Empty ? Guid.NewGuid() : context.RunId,
            parentCallId, profile.Id, profile.Name, profile.TriggerCondition, profile.SystemPrompt,
            provider.Id, provider.Name, provider.Protocol, model.Id, model.ModelId,
            model.ContextLength, model.MaximumOutputTokens, profile.ReasoningEffort,
            profile.ShareContext, repository?.InstanceId ?? Guid.NewGuid().ToString("N"),
            SubagentRunStatus.Failed,
            "子代理运行记录不可用，未启动模型请求。", errorCode, now, now, now);
    }

    private SubagentRun Terminal(SubagentRun run, SubagentRunStatus status, string summary, string? errorCode)
    {
        var now = _timeProvider.GetUtcNow();
        return run with
        {
            Status = status,
            Summary = summary,
            ErrorCode = errorCode,
            UpdatedAt = now,
            CompletedAt = now
        };
    }
}
