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
    ISubagentRunRepository? runs = null,
    TimeProvider? timeProvider = null) : IAgentTool
{
    public const string ToolName = "subagent.run";
    private const int MaximumTaskCharacters = 12_000;

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

        var lifecycle = new SubagentRunLifecycle(runs, timeProvider);
        SubagentRun run;
        try
        {
            run = await lifecycle.StartAsync(profile, provider, model, context, request.CallId,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var local = lifecycle.StartFailure(profile, provider, model, context, request.CallId,
                "subagent_run_unavailable");
            return ToolResult.Failure("无法保存子代理运行状态，未启动模型请求。",
                "subagent_run_unavailable") with { SubagentRun = local };
        }
        return await new SubagentRunExecutor(providers, lifecycle).RunAsync(
            profile, provider, model, task, context, request.CallId, run, cancellationToken);
    }

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
