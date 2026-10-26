using System.Text.Json;
using MossAgent.Agent.Resources;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

public sealed class SubagentTool(
    ISubagentConfigurationRepository subagents,
    IConfigurationRepository configurations,
    IModelProviderRegistry providers,
    ISubagentRunJournal journal) : IAgentTool
{
    public const string ToolName = "subagent.run";
    private const int MaximumTaskCharacters = 12_000;
    private const int MaximumErrorCodeLength = 128;
    private static readonly TimeSpan JournalCompletionBudget = TimeSpan.FromSeconds(5);

    public ToolDescriptor Descriptor { get; } = new(
        ToolName,
        "将独立任务委派给符合适用场景的已启用子代理；其工具调用沿用主任务的授权、审批和审计。",
        """{"type":"object","properties":{"agent":{"type":"string"},"task":{"type":"string"}},"required":["agent","task"]}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Network);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var agentName = ReadArgument(request.Arguments, "agent");
        var task = ReadArgument(request.Arguments, "task");
        if (string.IsNullOrWhiteSpace(agentName) || string.IsNullOrWhiteSpace(task))
        {
            return ToolResult.Failure("必须提供子代理名称和待处理任务。", "invalid_arguments");
        }

        if (task.Length > MaximumTaskCharacters)
        {
            return ToolResult.Failure("子代理任务超过允许长度。", "subagent_task_too_long");
        }

        var profile = await FindProfileAsync(agentName, cancellationToken);
        if (profile is null)
        {
            return ToolResult.Failure("指定的子代理不存在或未启用。", "subagent_unavailable");
        }

        var provider = await FindProviderAsync(profile.ProviderId, cancellationToken);
        if (provider is null)
        {
            return ToolResult.Failure("子代理所选供应商不存在或已停用。", "subagent_provider_unavailable");
        }

        var model = await FindModelAsync(profile, cancellationToken);
        if (model is null)
        {
            return ToolResult.Failure("子代理所选模型不存在或已停用。", "subagent_model_unavailable");
        }

        if (!ModelReasoningPolicy.Supports(provider.Protocol, model.ModelId, profile.ReasoningEffort))
        {
            return ToolResult.Failure(SubagentText.Get("ReasoningUnavailable"), "subagent_reasoning_unavailable");
        }

        return await RunJournaledAsync(profile, provider, model, task, request, context, cancellationToken);
    }

    // The journal records the resolved run before any child model call. A missing task row
    // merely skips journaling; a storage failure is fail-closed so no run goes unaccounted
    // when the record cannot even be attempted.
    private async Task<ToolResult> RunJournaledAsync(
        SubagentProfile profile, AiProvider provider, ModelProfile model, string task,
        ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var record = new SubagentRunRecord(runId, context.TaskId, request.CallId,
            SubagentRunSnapshot.ProfileLabel(profile),
            SubagentRunSnapshot.Create(profile, provider, model, task, context),
            SubagentRunStatus.Running, null, now, now);
        bool journaled;
        try
        {
            journaled = await journal.BeginAsync(record, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return ToolResult.Failure(SubagentText.Get("JournalUnavailable"), "subagent_journal_unavailable");
        }

        try
        {
            var result = await new SubagentRunExecutor(providers).RunAsync(
                profile, provider, model, task, context, request.CallId, cancellationToken);
            await CompleteBestEffortAsync(journaled, runId,
                result.IsSuccess ? SubagentRunStatus.Completed : SubagentRunStatus.Failed,
                result.IsSuccess ? null : BoundedErrorCode(result.ErrorCode));
            return result;
        }
        catch (OperationCanceledException)
        {
            await CompleteBestEffortAsync(journaled, runId, SubagentRunStatus.Cancelled, null);
            throw;
        }
    }

    private async Task CompleteBestEffortAsync(bool journaled, Guid runId,
        SubagentRunStatus status, string? errorCode)
    {
        if (!journaled) return;
        using var deadline = new CancellationTokenSource(JournalCompletionBudget);
        try { await journal.CompleteAsync(runId, status, errorCode, deadline.Token); }
        catch (Exception) { /* Best-effort: a terminal mark must never mask a finished run. */ }
    }

    private static string? BoundedErrorCode(string? errorCode) =>
        string.IsNullOrEmpty(errorCode) ? null
            : errorCode.Length <= MaximumErrorCodeLength ? errorCode : errorCode[..MaximumErrorCodeLength];

    private async Task<SubagentProfile?> FindProfileAsync(
        string name,
        CancellationToken cancellationToken) =>
        (await subagents.GetAllAsync(cancellationToken))
            .Where(static item => item.IsEnabled)
            .Take(16)
            .FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private async Task<AiProvider?> FindProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken) =>
        (await configurations.GetProvidersAsync(cancellationToken))
            .FirstOrDefault(item => item.Id == providerId && item.IsEnabled);

    private async Task<ModelProfile?> FindModelAsync(
        SubagentProfile profile,
        CancellationToken cancellationToken) =>
        (await configurations.GetModelsAsync(profile.ProviderId, cancellationToken))
            .FirstOrDefault(item => item.Id == profile.ModelProfileId && item.ProviderId == profile.ProviderId && item.IsEnabled);

    private static string ReadArgument(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
