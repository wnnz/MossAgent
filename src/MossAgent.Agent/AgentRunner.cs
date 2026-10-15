using System.Runtime.CompilerServices;
using System.Text.Json;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Agent.Resources;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

public sealed class AgentRunner(
    IModelProviderRegistry providers,
    IToolCatalog tools,
    IToolExecutor executor,
    ISubagentConfigurationRepository subagents) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enabledSubagents = !request.ToolContext.ToolsEnabled ? [] : (await subagents.GetAllAsync(cancellationToken))
            .Where(static profile => profile.IsEnabled)
            .Take(16)
            .ToArray();
        var messages = request.Messages.ToList();
        var systemPrompt = AgentSystemPromptBuilder.Build(request.ToolContext, request.AgentInstructions)
            + SubagentCatalogPromptBuilder.Build(enabledSubagents);
        messages.Insert(0, new ModelMessage(ModelRole.System, systemPrompt));
        await foreach (var agentEvent in RunTurnsAsync(
                           request, messages, enabledSubagents.Length > 0, cancellationToken))
        {
            yield return agentEvent;
        }
    }

    private async IAsyncEnumerable<AgentEvent> RunTurnsAsync(
        AgentRunRequest request,
        List<ModelMessage> messages,
        bool hasSubagents,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var turn = 1; turn <= request.MaximumTurns; turn++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = new ModelTurnState();
            var descriptors = request.ToolContext.ToolsEnabled ? GetDescriptors(hasSubagents) : [];
            await foreach (var agentEvent in StreamTurnAsync(
                               request, messages, descriptors, turn, state, cancellationToken))
            {
                yield return agentEvent;
            }

            if (state.Failed)
            {
                yield break;
            }

            if (state.Calls.Count == 0)
            {
                yield return new AgentCompletedEvent();
                yield break;
            }

            if (!request.ToolContext.ToolsEnabled)
            {
                yield return new AgentFailureEvent("tools_disabled", AgentText.Get("ProjectlessToolsUnavailable"));
                yield break;
            }

            AppendAssistantToolCall(messages, state);
            var context = request.ToolContext with
            {
                ConversationContext = !hasSubagents
                    ? null
                    : SubagentConversationContextBuilder.Build(messages),
                SubagentTools = hasSubagents
                    ? new SubagentToolGateway(tools, executor)
                    : null
            };
            await foreach (var agentEvent in ExecuteCallsAsync(
                               state.Calls, context, messages, cancellationToken))
            {
                yield return agentEvent;
            }
        }

        yield return new AgentFailureEvent("turn_limit", "Agent 已达到最大工具轮次限制。");
    }

    private IReadOnlyList<ToolDescriptor> GetDescriptors(bool hasSubagents)
    {
        var descriptors = tools.Descriptors;
        return hasSubagents
            ? descriptors.ToArray()
            : descriptors.Where(static tool => !tool.Name.Equals(
                SubagentTool.ToolName, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private async IAsyncEnumerable<AgentEvent> StreamTurnAsync(
        AgentRunRequest request,
        IReadOnlyList<ModelMessage> messages,
        IReadOnlyList<ToolDescriptor> descriptors,
        int turn,
        ModelTurnState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AgentStatusEvent($"模型处理中 · 第 {turn} 轮");
        var contextMessages = ModelContextWindowBuilder.Build(messages, descriptors, request.Model);
        var modelRequest = new ModelRequest(request.Provider, request.Model, contextMessages, descriptors);
        var provider = providers.Get(request.Provider.Protocol);

        await foreach (var modelEvent in provider.StreamAsync(modelRequest, cancellationToken))
        {
            switch (modelEvent)
            {
                case TextDelta text:
                    state.AssistantText.Append(text.Text);
                    yield return new AgentTextEvent(text.Text);
                    break;
                case ToolCallCompleted call:
                    state.Calls.Add(call);
                    break;
                case ModelCompleted completed:
                    state.Continuation = completed.Continuation;
                    break;
                case ModelFailed failure:
                    state.Failed = true;
                    yield return new AgentFailureEvent(failure.Code, failure.Message);
                    break;
            }
        }
    }

    private static void AppendAssistantToolCall(List<ModelMessage> messages, ModelTurnState state) =>
        messages.Add(new ModelMessage(
            ModelRole.Assistant,
            state.AssistantText.ToString(),
            ToolCalls: state.Calls.Select(static call => new ModelToolCall(
                call.CallId, call.ToolName, call.Arguments, call.ProviderCallId)).ToArray(),
            Continuation: state.Continuation));

    private async IAsyncEnumerable<AgentEvent> ExecuteCallsAsync(
        IReadOnlyList<ToolCallCompleted> calls,
        ToolExecutionContext context,
        List<ModelMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var call in calls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AgentStatusEvent($"执行工具 · {call.ToolName}");
            var toolRequest = new ToolRequest(call.CallId, call.ToolName, call.Arguments);
            ToolResult? result = null;
            await foreach (var agentEvent in AgentToolCallStream.ExecuteAsync(executor, toolRequest, context, cancellationToken))
            {
                if (agentEvent is AgentToolEvent completed) result = completed.Result;
                yield return agentEvent;
            }
            if (result is null) throw new InvalidOperationException("Tool stream did not return a final result.");
            cancellationToken.ThrowIfCancellationRequested();
            messages.Add(new ModelMessage(
                ModelRole.Tool,
                JsonSerializer.Serialize(new { result.IsSuccess, result.Summary, result.Content }),
                call.CallId,
                call.ToolName));
        }
    }
}
