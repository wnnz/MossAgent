using System.Net;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiCompatibleProviderTests
{
    [Fact]
    public async Task StreamAsync_UsesChatCompletionsAndParsesTextAndToolCalls()
    {
        var tool = new ToolDescriptor(
            "filesystem.read_file", "read", "{\"type\":\"object\"}",
            ToolRiskLevel.ReadOnly, ToolCapability.FileRead);
        var toolNames = new ProviderToolNameMap([tool]);
        var wireToolName = toolNames.Encode(tool.Name);
        var responseBody = $$$"""
            data: {"id":"chat-1","choices":[{"index":0,"delta":{"content":"hello"},"finish_reason":null}]}

            data: {"id":"chat-1","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call-1","type":"function","function":{"name":"{{{wireToolName}}}","arguments":"{\"path\":\""}}]},"finish_reason":null}]}

            data: {"id":"chat-1","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"a.txt\"}"}}]},"finish_reason":"tool_calls"}]}

            data: [DONE]

            """;
        var handler = new RecordingResponseHandler(
            HttpStatusCode.OK, responseBody, contentType: "text/event-stream");
        var clients = new StaticProviderHttpClientFactory(handler);
        var provider = new OpenAiCompatibleProvider(clients);
        var events = new List<ModelEvent>();

        await foreach (var modelEvent in provider.StreamAsync(
            CreateRequest(tool), TestContext.Current.CancellationToken))
        {
            events.Add(modelEvent);
        }

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("https://example.test/v1/chat/completions", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer fixture-api-key", handler.AuthorizationHeader);
        Assert.Equal("hello", Assert.IsType<TextDelta>(events[0]).Text);
        var toolCall = Assert.IsType<ToolCallCompleted>(events[1]);
        Assert.Equal("filesystem.read_file", toolCall.ToolName);
        Assert.Equal("a.txt", toolCall.Arguments.GetProperty("path").GetString());
        Assert.Equal("chat-1", Assert.IsType<ModelCompleted>(events[2]).ResponseId);
    }

    [Fact]
    public async Task StreamAsync_SendsBoundedAdvancedParametersToHttpClient()
    {
        var tool = new ToolDescriptor("filesystem.read_file", "read", "{\"type\":\"object\"}",
            ToolRiskLevel.ReadOnly, ToolCapability.FileRead);
        var request = CreateRequest(tool);
        request = request with
        {
            Model = request.Model with
            {
                AdvancedParametersJson =
                    "{\"messages\":[],\"max_completion_tokens\":999999,\"metadata\":{\"tag\":\"safe\"}}"
            }
        };
        var handler = new RecordingResponseHandler(HttpStatusCode.OK,
            "data: [DONE]\n\n", contentType: "text/event-stream");
        var clients = new StaticProviderHttpClientFactory(handler);
        var provider = new OpenAiCompatibleProvider(clients);

        await foreach (var _ in provider.StreamAsync(request, TestContext.Current.CancellationToken))
        {
        }

        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var body = document.RootElement;
        Assert.Single(body.GetProperty("messages").EnumerateArray());
        Assert.False(body.TryGetProperty("max_tokens", out _));
        Assert.Equal(1_024, body.GetProperty("max_completion_tokens").GetInt32());
        Assert.Equal("safe", body.GetProperty("metadata").GetProperty("tag").GetString());
    }

    private static ModelRequest CreateRequest(ToolDescriptor tool)
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(
            providerId, "Compatible fixture", ProviderProtocol.OpenAiCompatible,
            new Uri("https://example.test/v1/"), "fixture-api-key", null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), providerId, "fixture-model", "Fixture model", 8_192, 1_024,
            "off", null, null, false, false, true, true, "{}");
        return new ModelRequest(
            provider, model, [new ModelMessage(ModelRole.User, "hello")], [tool]);
    }
}
