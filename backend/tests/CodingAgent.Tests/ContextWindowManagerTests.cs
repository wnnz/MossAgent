using CodingAgent.Agent;
using CodingAgent.Domain.Models;
using Xunit;

namespace CodingAgent.Tests;

public class ContextWindowManagerTests
{
    private readonly ContextWindowManager _manager = new();

    [Fact]
    public void EstimateTokens_ApproximatesCharsOverFour()
    {
        Assert.Equal(10, _manager.EstimateTokens(new string('a', 40)));
        Assert.Equal(0, _manager.EstimateTokens((string?)null));
        Assert.Equal(0, _manager.EstimateTokens(string.Empty));
    }

    [Fact]
    public void Trim_SmallHistory_ReturnsUnchanged()
    {
        var messages = new List<LlmMessage>
        {
            new("system", "sys"),
            new("user", "hello"),
            new("assistant", "hi"),
        };
        var trimmed = _manager.Trim(messages, 100_000);
        Assert.Equal(messages.Count, trimmed.Count);
    }

    [Fact]
    public void Trim_LargeHistory_KeepsSystemAndRecent()
    {
        var messages = new List<LlmMessage> { new("system", "sys") };
        for (var i = 0; i < 200; i++)
        {
            messages.Add(new LlmMessage("user", new string('x', 2000)));
        }

        var trimmed = _manager.Trim(messages, 20_000);
        Assert.True(trimmed.Count < messages.Count);
        Assert.Equal("system", trimmed[0].Role);
        // 插入了裁剪占位
        Assert.Contains(trimmed, m => (m.Content ?? string.Empty).Contains("被裁剪"));
    }

    [Fact]
    public void TruncateToolOutput_LongToolOutput_Truncated()
    {
        var longOutput = new string('y', 20_000);
        var message = new LlmMessage("tool", longOutput) { ToolCallId = "t1", Name = "grep" };
        var truncated = _manager.TruncateToolOutput(message);
        Assert.True(truncated.Content!.Length < 10_000);
        Assert.Contains("已截断", truncated.Content);
        Assert.Equal("t1", truncated.ToolCallId);
    }

    [Fact]
    public void TruncateToolOutput_ShortOutput_Unchanged()
    {
        var message = new LlmMessage("tool", "short");
        var result = _manager.TruncateToolOutput(message);
        Assert.Equal("short", result.Content);
    }
}
