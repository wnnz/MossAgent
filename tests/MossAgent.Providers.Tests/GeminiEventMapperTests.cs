using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiEventMapperTests
{
    [Fact]
    public void Map_HandlesTextToolAndFinishReason()
    {
        var descriptor = new ToolDescriptor(
            "browser.navigate", "navigate", "{\"type\":\"object\"}",
            ToolRiskLevel.ExternalSideEffect, ToolCapability.BrowserWrite);
        var names = new ProviderToolNameMap([descriptor]);
        var payload = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new object[]
                        {
                            new { text = "hello" },
                            new
                            {
                                functionCall = new
                                {
                                    name = names.Encode(descriptor.Name),
                                    args = new { url = "https://example.com" }
                                }
                            }
                        }
                    },
                    finishReason = "STOP"
                }
            }
        });
        using var document = JsonDocument.Parse(payload);

        var events = GeminiEventMapper.Map(document.RootElement, names);

        Assert.Equal("hello", Assert.IsType<TextDelta>(events[0]).Text);
        var tool = Assert.IsType<ToolCallCompleted>(events[1]);
        Assert.Equal(descriptor.Name, tool.ToolName);
        Assert.IsType<ModelCompleted>(events[2]);
    }

    [Fact]
    public void Map_HandlesError()
    {
        using var document = JsonDocument.Parse(
            "{\"error\":{\"code\":429,\"message\":\"rate limited\"}}");

        var failure = Assert.IsType<ModelFailed>(Assert.Single(
            GeminiEventMapper.Map(document.RootElement, new ProviderToolNameMap([]))));

        Assert.Equal("429", failure.Code);
        Assert.Equal("rate limited", failure.Message);
    }
}
