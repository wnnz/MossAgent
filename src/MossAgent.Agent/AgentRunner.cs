using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

public sealed class AgentRunner(
    IModelProviderRegistry providers,
    IToolCatalog tools,
    IToolExecutor executor) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = request.Messages.ToList();
        messages.Insert(0, new ModelMessage(
            ModelRole.System,
            AgentSystemPromptBuilder.Build(request.ToolContext)));
        var provider = providers.Get(request.Provider.Protocol);

        for (var turn = 1; turn <= request.MaximumTurns; turn++)
        {
            yield return new AgentStatusEvent($"模型处理中 · 第 {turn} 轮");
            var descriptors = tools.Descriptors.ToArray();
            var contextMessages = ModelContextWindowBuilder.Build(
                messages, descriptors, request.Model);
            var modelRequest = new ModelRequest(
                request.Provider, request.Model, contextMessages, descriptors);
            var calls = new List<ToolCallCompleted>();
            var assistantText = new StringBuilder();
            var failed = false;

            await foreach (var modelEvent in provider.StreamAsync(modelRequest, cancellationToken))
            {
                switch (modelEvent)
                {
                    case TextDelta text:
                        assistantText.Append(text.Text);
                        yield return new AgentTextEvent(text.Text);
                        break;
                    case ToolCallCompleted call:
                        calls.Add(call);
                        break;
                    case ModelCompleted:
                        break;
                    case ModelFailed failure:
                        failed = true;
                        yield return new AgentFailureEvent(failure.Code, failure.Message);
                        break;
                }
            }

            if (failed)
            {
                yield break;
            }

            if (calls.Count == 0)
            {
                yield return new AgentCompletedEvent();
                yield break;
            }

            messages.Add(new ModelMessage(
                ModelRole.Assistant,
                assistantText.ToString(),
                ToolCalls: calls.Select(static call => new ModelToolCall(
                    call.CallId, call.ToolName, call.Arguments)).ToArray()));
            foreach (var call in calls)
            {
                yield return new AgentStatusEvent($"执行工具 · {call.ToolName}");
                var toolRequest = new ToolRequest(call.CallId, call.ToolName, call.Arguments);
                var result = await executor.ExecuteAsync(toolRequest, request.ToolContext, cancellationToken);
                yield return new AgentToolEvent(call.ToolName, call.CallId, result);
                messages.Add(new ModelMessage(
                    ModelRole.Tool,
                    JsonSerializer.Serialize(new { result.IsSuccess, result.Summary, result.Content }),
                    call.CallId,
                    call.ToolName));
            }
        }

        yield return new AgentFailureEvent("turn_limit", "Agent 已达到最大工具轮次限制。");
    }
}
