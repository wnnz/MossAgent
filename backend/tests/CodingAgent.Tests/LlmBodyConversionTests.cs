using System.Text.Json;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Llm;
using Xunit;

namespace CodingAgent.Tests;

public class LlmBodyConversionTests
{
    private static JsonElement ToJson(object body) =>
        JsonSerializer.SerializeToElement(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void OpenAiBuildBody_ToolMessage_OutputsToolCallId()
    {
        var request = new LlmRequest
        {
            Model = "m",
            Messages =
            [
                new LlmMessage("system", "sys"),
                new LlmMessage("user", "hi"),
                new LlmMessage("assistant", null)
                {
                    ToolCalls = [new LlmToolCall("call-1", "echo", """{"text":"x"}""")],
                },
                new LlmMessage("tool", "result-text") { ToolCallId = "call-1", Name = "echo" },
            ],
            Tools = [new LlmToolDefinition("echo", "回显", """{"type":"object"}""")],
        };

        var json = ToJson(OpenAiCompatibleClient.BuildBody(request));
        var messages = json.GetProperty("messages");

        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("tool", messages[3].GetProperty("role").GetString());
        Assert.Equal("call-1", messages[3].GetProperty("tool_call_id").GetString());
        Assert.Equal("result-text", messages[3].GetProperty("content").GetString());

        var toolCalls = messages[2].GetProperty("tool_calls");
        Assert.Equal(1, toolCalls.GetArrayLength());
        Assert.Equal("echo", toolCalls[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("""{"text":"x"}""", toolCalls[0].GetProperty("function").GetProperty("arguments").GetString());

        Assert.Equal(1, json.GetProperty("tools").GetArrayLength());
    }

    [Fact]
    public void OpenAiBuildBody_ReasoningOff_OmitsReasoningEffort()
    {
        var request = new LlmRequest { Model = "m", ReasoningEffort = ReasoningEffort.Off };
        var json = ToJson(OpenAiCompatibleClient.BuildBody(request));
        Assert.False(json.TryGetProperty("reasoning_effort", out _));
    }

    [Fact]
    public void OpenAiBuildBody_ReasoningMedium_IncludesReasoningEffort()
    {
        var request = new LlmRequest { Model = "m", ReasoningEffort = ReasoningEffort.Medium };
        var json = ToJson(OpenAiCompatibleClient.BuildBody(request));
        Assert.Equal("medium", json.GetProperty("reasoning_effort").GetString());
    }

    [Fact]
    public void AnthropicBuildBody_SystemLifted_ToolResultsMerged()
    {
        var request = new LlmRequest
        {
            Model = "m",
            MaxOutputTokens = 8192,
            Messages =
            [
                new LlmMessage("system", "sys prompt"),
                new LlmMessage("user", "hi"),
                new LlmMessage("assistant", null)
                {
                    ToolCalls = [new LlmToolCall("call-1", "echo", """{"text":"x"}""")],
                },
                new LlmMessage("tool", "r1") { ToolCallId = "call-1", Name = "echo" },
                new LlmMessage("tool", "r2") { ToolCallId = "call-2", Name = "echo" },
                new LlmMessage("user", "thanks"),
            ],
        };

        var json = ToJson(AnthropicClient.BuildBody(request));

        // system 顶格
        Assert.Equal("sys prompt", json.GetProperty("system").GetString());

        // messages: user(hi) → assistant(tool_use) → user(2 个 tool_result) → user(thanks)
        var messages = json.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());

        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        var blocks = messages[1].GetProperty("content");
        Assert.Equal("tool_use", blocks[0].GetProperty("type").GetString());
        Assert.Equal("call-1", blocks[0].GetProperty("id").GetString());
        Assert.Equal("x", blocks[0].GetProperty("input").GetProperty("text").GetString());

        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        var results = messages[2].GetProperty("content");
        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal("tool_result", results[0].GetProperty("type").GetString());
        Assert.Equal("call-1", results[0].GetProperty("tool_use_id").GetString());
        Assert.Equal("r1", results[0].GetProperty("content").GetString());
        Assert.Equal("call-2", results[1].GetProperty("tool_use_id").GetString());

        Assert.Equal("user", messages[3].GetProperty("role").GetString());
    }

    [Fact]
    public void AnthropicBuildBody_ThinkingBudgetAutoRaisesMaxTokens()
    {
        var request = new LlmRequest { Model = "m", MaxOutputTokens = 8192, ReasoningEffort = ReasoningEffort.Medium };
        var json = ToJson(AnthropicClient.BuildBody(request));
        // budget 8192 < 8192 不成立 → budget(8192) >= maxTokens 时抬高
        var thinking = json.GetProperty("thinking");
        Assert.Equal("enabled", thinking.GetProperty("type").GetString());
        Assert.Equal(8192, thinking.GetProperty("budget_tokens").GetInt32());
        Assert.Equal(8192 + 4096, json.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public void CombineUrl_AvoidsV1DoubleJoin()
    {
        Assert.Equal("https://api.example.com/v1/chat/completions",
            OpenAiCompatibleClient.CombineUrl("https://api.example.com/v1", "/v1/chat/completions"));
        Assert.Equal("https://api.example.com/v1/chat/completions",
            OpenAiCompatibleClient.CombineUrl("https://api.example.com", "/v1/chat/completions"));
        Assert.Equal("https://api.example.com/v1/models",
            OpenAiCompatibleClient.CombineUrl("https://api.example.com/v1/", "/v1/models"));
    }
}
