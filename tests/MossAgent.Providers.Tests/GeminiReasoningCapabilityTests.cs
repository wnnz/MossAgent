using System.Net;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Gemini;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiReasoningCapabilityTests
{
    private const string ResponseBody = "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]},\"finishReason\":\"STOP\"}]}\n\n";

    [Theory]
    [InlineData("gemini-3.8-flash", "minimal")]
    [InlineData("gemini-3.7-flash", "none")]
    [InlineData("gemini-3.1-pro-preview", "minimal")]
    [InlineData("gemini-3-pro-preview", "medium")]
    [InlineData("gemini-3.1-flash-lite-image", "low")]
    [InlineData("gemini-3.1-flash-lite-image", "medium")]
    [InlineData("gemini-3.5-flash", "none")]
    [InlineData("gemini-3.6-flash", "off")]
    [InlineData("gemini-3.8-flash", "xhigh")]
    [InlineData("gemini-3.8-flash", "max")]
    [InlineData("gemini-3.8-flash", "ultra")]
    [InlineData("gemini-2.5-pro", "none")]
    [InlineData("gemini-2.5-pro", "off")]
    [InlineData("gemini-2.5-flash", "ultra")]
    public async Task UnsupportedEffort_IsRejectedWithoutHttpOrEchoingConfiguration(string modelId, string effort)
    {
        var handler = new RecordingResponseHandler(HttpStatusCode.OK, ResponseBody, contentType: "text/event-stream");
        var clients = new StaticProviderHttpClientFactory(handler);
        var provider = new GeminiGenerateContentProvider(clients);
        var events = new List<ModelEvent>();

        await foreach (var modelEvent in provider.StreamAsync(
            CreateRequest(modelId, effort), TestContext.Current.CancellationToken))
            events.Add(modelEvent);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("unsupported_reasoning_effort", failure.Code);
        Assert.Null(handler.RequestUri);
        Assert.DoesNotContain("fixture-secret", failure.Message);
        Assert.DoesNotContain(modelId, failure.Message);
    }

    [Theory]
    [InlineData("gemini-3.8-flash", "low", "LOW")]
    [InlineData("gemini-3.7-flash", "medium", "MEDIUM")]
    [InlineData("gemini-3.1-pro-preview", "medium", "MEDIUM")]
    [InlineData("gemini-3-pro-preview", "high", "HIGH")]
    [InlineData("gemini-3.1-flash-lite-image", "minimal", "MINIMAL")]
    [InlineData("gemini-3.5-flash-lite", "minimal", "MINIMAL")]
    [InlineData("gemini-3-flash-preview", "minimal", "MINIMAL")]
    [InlineData("gemini-3.6-flash", "minimal", "MINIMAL")]
    [InlineData("gemini-3.8-flash", " HIGH ", "HIGH")]
    public async Task SupportedEffort_ReachesHttpWithoutChangingLevel(string modelId, string effort, string expected)
    {
        var handler = new RecordingResponseHandler(HttpStatusCode.OK, ResponseBody, contentType: "text/event-stream");
        var clients = new StaticProviderHttpClientFactory(handler);
        var provider = new GeminiGenerateContentProvider(clients);
        var events = new List<ModelEvent>();

        await foreach (var modelEvent in provider.StreamAsync(
            CreateRequest(modelId, effort), TestContext.Current.CancellationToken))
            events.Add(modelEvent);

        Assert.Contains(events, modelEvent => modelEvent is ModelCompleted);
        Assert.DoesNotContain(events, modelEvent => modelEvent is ModelFailed);
        using var document = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(expected, document.RootElement.GetProperty("generationConfig")
            .GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    private static ModelRequest CreateRequest(string modelId, string effort)
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(providerId, "Gemini fixture", ProviderProtocol.GeminiGenerateContent,
            new Uri("https://example.test/v1beta/"), "fixture-secret", null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), providerId, modelId, "Fixture",
            128_000, 8_192, effort, null, null, false, false, true, true, "{}");
        return new ModelRequest(provider, model, [new ModelMessage(ModelRole.User, "hello")], []);
    }
}
