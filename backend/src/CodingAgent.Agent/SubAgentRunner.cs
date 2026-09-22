using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CodingAgent.Agent;

/// <summary>
/// 子代理执行器：独立 AgentLoop 运行，使用子代理配置的提供商/模型/思考强度/最大轮次/允许工具（禁 task 防递归）。
/// 通过 IServiceProvider 惰性解析 AgentLoop，打破 DI 循环依赖（ToolRegistry→TaskTool→SubAgentRunner→AgentLoop→ToolRegistry）。
/// </summary>
public class SubAgentRunner(IServiceProvider services) : ISubAgentRunner
{
    public async Task<string> RunAsync(SubAgent subAgent, string input, CancellationToken ct = default)
    {
        // 运行时解析：避免构造期循环依赖
        var loop = services.GetRequiredService<AgentLoop>();
        var allowedTools = ParseAllowedTools(subAgent.AllowedToolsJson);
        // 禁 task 防递归；空列表视为不限制（null）
        if (allowedTools is { Count: > 0 })
        {
            allowedTools = allowedTools.Where(t => t != "task").ToList();
            if (allowedTools.Count == 0)
            {
                allowedTools = null;
            }
        }

        // 子代理不持久化消息（PersistMessages=false），不触达会话存储与主提示词构建
        var session = new Session
        {
            Id = Guid.NewGuid(),
            ProviderId = subAgent.ProviderId,
            ModelId = subAgent.ModelId,
            ReasoningEffort = subAgent.ReasoningEffort,
        };

        var options = new AgentLoopOptions
        {
            MaxTurns = Math.Max(1, subAgent.MaxTurns),
            AllowedToolNames = allowedTools,
            SystemPromptOverride = BuildSubAgentPrompt(subAgent),
            PersistMessages = false,
        };

        var finalText = string.Empty;
        await foreach (var evt in loop.RunAsync(session, input, null, options, ct))
        {
            if (evt.Type == AgentEventType.TurnCompleted && evt.Data is { } data)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("content", out var content))
                {
                    finalText = content.GetString() ?? string.Empty;
                }
            }
            else if (evt.Type == AgentEventType.Error && evt.Data is { } err)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(err);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var message))
                {
                    finalText = $"[子代理错误] {message.GetString()}";
                }
            }
        }
        return finalText;
    }

    private static string BuildSubAgentPrompt(SubAgent subAgent) =>
        $"""
        你是子代理「{subAgent.Name}」。专注完成主代理交给你的任务，直接返回最终结果文本，不与用户直接交互。
        {(string.IsNullOrWhiteSpace(subAgent.SystemPrompt) ? string.Empty : subAgent.SystemPrompt)}
        """;

    private static List<string>? ParseAllowedTools(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
