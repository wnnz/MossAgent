using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class HistoricalToolObservationRequestTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    public void HistoryRemainsPlainUserDataEvenIfNativeCallMetadataIsAccidentallySupplied(ProviderProtocol protocol)
    {
        var observation = HistoricalToolObservation.Create("unique history data", "historical-id") with
        {
            ToolCallId = "must-not-be-replayed",
            ToolName = "missing.deleted.tool",
            ToolCalls = [new ModelToolCall("must-not-be-replayed", "missing.deleted.tool", JsonSerializer.SerializeToElement(new { }))]
        };
        var provider = new AiProvider(Guid.NewGuid(), "fixture", protocol, new Uri("https://fixture.example.test/"),
            "synthetic-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 32000, 1024,
            "off", null, null, false, false, true, true, "{}");
        var request = new ModelRequest(provider, model, [observation, new(ModelRole.User, "continue")], []);
        var names = new ProviderToolNameMap([]);
        var json = protocol switch
        {
            ProviderProtocol.OpenAiResponses => OpenAiRequestBuilder.Build(request, names),
            ProviderProtocol.OpenAiCompatible => OpenAiCompatibleRequestBuilder.Build(request, names),
            ProviderProtocol.GeminiGenerateContent => GeminiRequestBuilder.Build(request, names),
            _ => AnthropicRequestBuilder.Build(request, names)
        };
        using var document = JsonDocument.Parse(json);
        var messages = document.RootElement.GetProperty(protocol switch
        {
            ProviderProtocol.OpenAiResponses => "input",
            ProviderProtocol.GeminiGenerateContent => "contents",
            _ => "messages"
        });
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Contains("unique history data", json);
        Assert.DoesNotContain("must-not-be-replayed", json);
        Assert.DoesNotContain("missing.deleted.tool", json);
        foreach (var item in messages.EnumerateArray())
        {
            Assert.False(item.TryGetProperty("tool_calls", out _));
            Assert.False(item.TryGetProperty("tool_call_id", out _));
            Assert.False(item.TryGetProperty("type", out _));
        }
    }
}
