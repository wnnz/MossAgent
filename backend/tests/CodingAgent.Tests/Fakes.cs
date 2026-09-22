using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Tests;

/// <summary>脚本化 Fake LLM 客户端：按调用次序返回预设事件序列。</summary>
internal class FakeLlmClient(List<List<LlmStreamEvent>> script) : ILlmClient
{
    private int _callIndex;

    public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken ct = default)
    {
        var events = script[Math.Min(_callIndex++, script.Count - 1)];
        return StreamAsyncImpl(events);
    }

    private static async IAsyncEnumerable<LlmStreamEvent> StreamAsyncImpl(List<LlmStreamEvent> events)
    {
        foreach (var evt in events)
        {
            await Task.Yield();
            yield return evt;
        }
    }
}

internal class FakeLlmClientFactory(ILlmClient client) : ILlmClientFactory
{
    public ILlmClient Create(Provider provider) => client;
}

/// <summary>固定回显工具（用于 AgentLoop 工具循环测试）。</summary>
internal class EchoTool : ITool
{
    public string Name => "echo";
    public string Description => "回显输入";
    public string ParametersSchemaJson => """{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""";

    public Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        var text = CodingAgent.Infrastructure.Tools.ToolArguments.GetString(argumentsJson, "text");
        return Task.FromResult(ToolResult.Ok($"echo: {text}"));
    }
}

internal class ThrowingMcpConnectionFactory : IMcpConnectionFactory
{
    public IMcpConnection Create(Domain.Entities.McpServer server) => throw new InvalidOperationException("测试中不应连接 MCP");
}
