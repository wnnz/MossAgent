using System.Text.Json;
using CodingAgent.Agent;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Database;
using CodingAgent.Infrastructure.Mcp;
using CodingAgent.Infrastructure.Repositories;
using CodingAgent.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodingAgent.Tests;

public class AgentLoopTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;

    public AgentLoopTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    private static JsonElement AsJson(object? data) => JsonSerializer.SerializeToElement(data, JsonOpts);

    private (AgentLoop Loop, ISessionRepository Sessions, Provider Provider) CreateLoop(List<List<LlmStreamEvent>> script)
    {
        var sessions = new SessionRepository(_db);
        var providers = new ProviderRepository(_db);
        var memories = new MemoryRepository(_db);
        var skills = new SkillRepository(_db);
        var subAgents = new SubAgentRepository(_db);
        var mcpRepo = new McpRepository(_db);

        var client = new FakeLlmClient(script);
        var toolRegistry = new ToolRegistry([new EchoTool()], new McpToolBridge(mcpRepo, new McpConnectionRegistry(new ThrowingMcpConnectionFactory())));
        var promptBuilder = new SystemPromptBuilder(memories, skills, subAgents, toolRegistry);

        var loop = new AgentLoop(
            new FakeLlmClientFactory(client),
            toolRegistry,
            sessions,
            providers,
            promptBuilder,
            new ContextWindowManager(),
            NullLogger<AgentLoop>.Instance);

        var provider = new Provider
        {
            Name = "fake",
            Type = ProviderType.OpenAiCompatible,
            BaseUrl = "http://localhost",
            Enabled = true,
            Models =
            [
                new ProviderModel { ModelId = "fake-model", SupportsTools = true },
            ],
        };
        providers.AddAsync(provider).GetAwaiter().GetResult();
        return (loop, sessions, provider);
    }

    [Fact]
    public async Task RunAsync_TwoTurnToolLoop_ThenFinalText()
    {
        var (loop, sessions, provider) = CreateLoop(
        [
            // 第一轮：调用 echo 工具
            [LlmStreamEvent.ToolCallOf(new LlmToolCall("call-1", "echo", """{"text":"hi"}"""))],
            // 第二轮：输出最终文本
            [LlmStreamEvent.DeltaOf("完成"), LlmStreamEvent.DeltaOf("了"), LlmStreamEvent.CompletedOf("stop")],
        ]);

        var session = new Session { Id = Guid.NewGuid(), ProviderId = provider.Id, ModelId = "fake-model" };
        await sessions.AddAsync(session);

        var events = new List<AgentEvent>();
        await foreach (var evt in loop.RunAsync(session, "测试任务", null, null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // 事件序列
        Assert.Contains(events, e => e.Type == AgentEventType.TurnStarted);
        var started = events.Where(e => e.Type == AgentEventType.ToolCallStarted).ToList();
        Assert.Single(started);
        Assert.Equal("echo", AsJson(started[0].Data).GetProperty("name").GetString());
        var finished = events.Where(e => e.Type == AgentEventType.ToolCallFinished).ToList();
        Assert.Single(finished);
        Assert.Equal("echo: hi", AsJson(finished[0].Data).GetProperty("result").GetString());
        Assert.False(AsJson(finished[0].Data).GetProperty("isError").GetBoolean());

        var completed = events.Last(e => e.Type == AgentEventType.TurnCompleted);
        Assert.Equal("完成了", AsJson(completed.Data).GetProperty("content").GetString());

        // 消息持久化：user → assistant(toolcall) → tool → assistant
        var messages = await sessions.GetMessagesAsync(session.Id);
        Assert.Equal(4, messages.Count);
        Assert.Equal(MessageRole.User, messages[0].Role);
        Assert.Equal(MessageRole.Assistant, messages[1].Role);
        Assert.NotNull(messages[1].ToolCallsJson);
        Assert.Equal(MessageRole.Tool, messages[2].Role);
        Assert.Equal("echo", messages[2].Name);
        Assert.Equal(MessageRole.Assistant, messages[3].Role);
        Assert.Equal("完成了", messages[3].Content);
    }

    [Fact]
    public async Task RunAsync_UnknownTool_ReturnsErrorResult()
    {
        var (loop, _, provider) = CreateLoop(
        [
            [LlmStreamEvent.ToolCallOf(new LlmToolCall("call-1", "nope", "{}"))],
            [LlmStreamEvent.DeltaOf("fallback"), LlmStreamEvent.CompletedOf("stop")],
        ]);

        var session = new Session { Id = Guid.NewGuid(), ProviderId = provider.Id, ModelId = "fake-model" };
        var events = new List<AgentEvent>();
        await foreach (var evt in loop.RunAsync(session, "x", null, null, CancellationToken.None))
        {
            events.Add(evt);
        }

        var finished = events.Where(e => e.Type == AgentEventType.ToolCallFinished).ToList();
        Assert.Single(finished);
        Assert.True(AsJson(finished[0].Data).GetProperty("isError").GetBoolean());
        Assert.Contains("工具不存在", AsJson(finished[0].Data).GetProperty("result").GetString());
    }

    [Fact]
    public async Task RunAsync_LlmError_EmitsErrorEvent()
    {
        var (loop, _, provider) = CreateLoop(
        [
            [LlmStreamEvent.ErrorOf("上游错误")],
        ]);

        var session = new Session { Id = Guid.NewGuid(), ProviderId = provider.Id, ModelId = "fake-model" };
        var events = new List<AgentEvent>();
        await foreach (var evt in loop.RunAsync(session, "x", null, null, CancellationToken.None))
        {
            events.Add(evt);
        }
        Assert.Contains(events, e => e.Type == AgentEventType.Error);
    }

    [Fact]
    public async Task RunAsync_AllowedToolRestriction_BlocksDisallowedTool()
    {
        var (loop, _, provider) = CreateLoop(
        [
            [LlmStreamEvent.ToolCallOf(new LlmToolCall("call-1", "echo", """{"text":"x"}"""))],
            [LlmStreamEvent.DeltaOf("done"), LlmStreamEvent.CompletedOf("stop")],
        ]);

        var session = new Session { Id = Guid.NewGuid(), ProviderId = provider.Id, ModelId = "fake-model" };
        var options = new AgentLoopOptions { AllowedToolNames = ["other-tool"] };

        var events = new List<AgentEvent>();
        await foreach (var evt in loop.RunAsync(session, "x", null, options, CancellationToken.None))
        {
            events.Add(evt);
        }

        var finished = events.Where(e => e.Type == AgentEventType.ToolCallFinished).ToList();
        Assert.Single(finished);
        Assert.True(AsJson(finished[0].Data).GetProperty("isError").GetBoolean());
        Assert.Contains("允许列表", AsJson(finished[0].Data).GetProperty("result").GetString());
    }

    public void Dispose() => _db.Dispose();
}
