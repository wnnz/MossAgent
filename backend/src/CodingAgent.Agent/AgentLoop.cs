using System.Text.Json;
using CodingAgent.Agent;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Tools;
using Microsoft.Extensions.Logging;

namespace CodingAgent.Agent;

/// <summary>Agent 循环选项（子代理运行时限制工具与轮次）。</summary>
public class AgentLoopOptions
{
    public int MaxTurns { get; set; } = 25;
    /// <summary>允许的工具名；null 表示全部。</summary>
    public IReadOnlyList<string>? AllowedToolNames { get; set; }
    public string? SystemPromptOverride { get; set; }
    public bool PersistMessages { get; set; } = true;
}

/// <summary>
/// Agent 循环：系统提示词 → 流式调用 LLM → 解析 tool_calls → 工具注册表顺序执行 → 结果回填继续，
/// 直至无工具调用或达到最大轮次。通过 IAsyncEnumerable 流式产出 AgentEvent。
/// </summary>
public class AgentLoop(
    ILlmClientFactory llmClientFactory,
    ToolRegistry toolRegistry,
    ISessionRepository sessionRepository,
    IProviderRepository providerRepository,
    SystemPromptBuilder promptBuilder,
    ContextWindowManager contextManager,
    ILogger<AgentLoop> logger)
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        Session session,
        string userMessage,
        ReasoningEffort? effortOverride = null,
        AgentLoopOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var opts = options ?? new AgentLoopOptions();
        var maxTurns = Math.Max(1, opts.MaxTurns);

        var provider = await providerRepository.GetWithModelsAsync(session.ProviderId, ct)
            ?? throw new InvalidOperationException($"提供商不存在: {session.ProviderId}");
        var model = provider.Models.FirstOrDefault(m => m.ModelId == session.ModelId)
            ?? provider.Models.FirstOrDefault();
        // 模型回退时同步实际请求的模型名
        var requestModel = model?.ModelId ?? session.ModelId;

        var client = llmClientFactory.Create(provider);
        var workspacePath = session.WorkspacePath ?? string.Empty;
        var toolContext = new ToolContext
        {
            SessionId = session.Id,
            WorkspacePath = workspacePath,
            AllowedToolNames = opts.AllowedToolNames,
        };

        // 历史消息
        var history = new List<LlmMessage>();
        if (opts.PersistMessages)
        {
            var stored = await sessionRepository.GetMessagesAsync(session.Id, ct);
            history = stored.Select(ToLlmMessage).ToList();
            var userMsg = new ChatMessage { SessionId = session.Id, Role = MessageRole.User, Content = userMessage, CreatedAt = DateTime.UtcNow };
            await sessionRepository.AddMessageAsync(userMsg, ct);
            history.Add(new LlmMessage("user", userMessage));
        }
        else
        {
            history.Add(new LlmMessage("user", userMessage));
        }

        var systemPrompt = opts.SystemPromptOverride
            ?? (workspacePath.Length > 0 ? await promptBuilder.BuildAsync(workspacePath, ct) : "你是 Coding Agent。");

        var maxContext = model?.MaxContextTokens ?? 128_000;
        var maxOutput = model?.MaxOutputTokens ?? 8_192;
        var reasoning = effortOverride ?? session.ReasoningEffort;

        var finalText = string.Empty;

        for (var turn = 1; turn <= maxTurns; turn++)
        {
            ct.ThrowIfCancellationRequested();
            yield return AgentEvent.Of(AgentEventType.TurnStarted, new { turnIndex = turn });

            var request = new LlmRequest
            {
                Model = requestModel,
                ReasoningEffort = reasoning,
                MaxOutputTokens = maxOutput,
                Messages = BuildRequestMessages(systemPrompt, history, maxContext),
                Tools = model is { SupportsTools: true } ? await toolRegistry.GetDefinitionsAsync(ct) : null,
            };

            var assistantText = new System.Text.StringBuilder();
            var toolCalls = new List<LlmToolCall>();
            string? error = null;

            await foreach (var evt in client.StreamAsync(request, ct))
            {
                switch (evt.Type)
                {
                    case LlmStreamEventType.Delta:
                        assistantText.Append(evt.Delta);
                        yield return AgentEvent.Of(AgentEventType.MessageDelta, new { content = evt.Delta });
                        break;
                    case LlmStreamEventType.ToolCall:
                        toolCalls.Add(evt.ToolCall!);
                        break;
                    case LlmStreamEventType.Error:
                        error = evt.Error;
                        break;
                    case LlmStreamEventType.Completed:
                        break;
                }
            }

            if (error is not null)
            {
                yield return AgentEvent.Of(AgentEventType.Error, new { message = error });
                yield break;
            }

            finalText = assistantText.ToString();

            // 保存 assistant 消息（含工具调用）
            if (opts.PersistMessages)
            {
                await sessionRepository.AddMessageAsync(new ChatMessage
                {
                    SessionId = session.Id,
                    Role = MessageRole.Assistant,
                    Content = finalText,
                    ToolCallsJson = toolCalls.Count > 0 ? JsonSerializer.Serialize(toolCalls) : null,
                    CreatedAt = DateTime.UtcNow,
                }, ct);
            }
            history.Add(new LlmMessage("assistant", finalText) { ToolCalls = toolCalls.Count > 0 ? toolCalls : null });

            if (toolCalls.Count == 0)
            {
                yield return AgentEvent.Of(AgentEventType.TurnCompleted, new { content = finalText });
                yield break;
            }

            // 顺序执行工具调用
            foreach (var call in toolCalls)
            {
                ct.ThrowIfCancellationRequested();
                yield return AgentEvent.Of(AgentEventType.ToolCallStarted, new
                {
                    id = call.Id,
                    name = call.Name,
                    arguments = call.ParseArguments(),
                });

                var toolResult = await ExecuteToolAsync(call, toolContext, ct);
                logger.LogInformation("工具 {Tool} 执行完成（成功={Success}）", call.Name, toolResult.Success);

                yield return AgentEvent.Of(AgentEventType.ToolCallFinished, new
                {
                    id = call.Id,
                    name = call.Name,
                    result = toolResult.Output,
                    isError = !toolResult.Success,
                });

                if (opts.PersistMessages)
                {
                    await sessionRepository.AddMessageAsync(new ChatMessage
                    {
                        SessionId = session.Id,
                        Role = MessageRole.Tool,
                        Content = toolResult.Output,
                        ToolCallId = call.Id,
                        Name = call.Name,
                        CreatedAt = DateTime.UtcNow,
                    }, ct);
                }
                history.Add(new LlmMessage("tool", toolResult.Output) { ToolCallId = call.Id, Name = call.Name });
            }

            if (turn == maxTurns)
            {
                yield return AgentEvent.Of(AgentEventType.Error, new { message = $"已达最大轮次 {maxTurns}，强制结束" });
            }
        }
    }

    private List<LlmMessage> BuildRequestMessages(string systemPrompt, List<LlmMessage> history, int maxContext)
    {
        var messages = new List<LlmMessage> { new("system", systemPrompt) };
        messages.AddRange(history);
        return contextManager.Trim(messages, maxContext);
    }

    private async Task<ToolResult> ExecuteToolAsync(LlmToolCall call, ToolContext context, CancellationToken ct)
    {
        var tools = await toolRegistry.GetToolsAsync(ct);
        var tool = tools.FirstOrDefault(t => t.Name == call.Name);
        if (tool is null)
        {
            return ToolResult.Fail($"工具不存在: {call.Name}");
        }
        if (context.AllowedToolNames is { } allowed && !allowed.Contains(tool.Name))
        {
            return ToolResult.Fail($"工具不在允许列表中: {tool.Name}");
        }
        try
        {
            return await tool.ExecuteAsync(call.ArgumentsJson, context, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "工具 {Tool} 执行异常", call.Name);
            return ToolResult.Fail($"工具执行异常: {ex.Message}");
        }
    }

    internal static LlmMessage ToLlmMessage(ChatMessage message) => message.Role switch
    {
        MessageRole.Assistant when message.ToolCallsJson is not null => new LlmMessage("assistant", message.Content)
        {
            ToolCalls = SafeDeserialize(message.ToolCallsJson),
        },
        MessageRole.Tool => new LlmMessage("tool", message.Content) { ToolCallId = message.ToolCallId, Name = message.Name },
        MessageRole.System => new LlmMessage("system", message.Content),
        MessageRole.User => new LlmMessage("user", message.Content),
        _ => new LlmMessage("assistant", message.Content),
    };

    private static List<LlmToolCall>? SafeDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<LlmToolCall>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
