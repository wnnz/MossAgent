using System.Net;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Gemini;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiGenerateContentProviderTests
{
    [Fact]
    public async Task StreamAsync_UsesGeminiEndpointAndKey_AndParsesSseEvents()
    {
        const string responseBody = "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Gemini stream response\"}]},\"finishReason\":\"STOP\"}]}\n\n";
        var handler = new RecordingResponseHandler(
            HttpStatusCode.OK,
            responseBody,
            contentType: "text/event-stream");
        var clients = new StaticProviderHttpClientFactory(handler);
        var provider = new GeminiGenerateContentProvider(clients);
        var events = new List<ModelEvent>();

        await foreach (var modelEvent in provider.StreamAsync(
            CreateRequest(), TestContext.Current.CancellationToken))
        {
            events.Add(modelEvent);
        }

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal(
            "https://example.test/v1beta/models/gemini-3.8-flash:streamGenerateContent?alt=sse",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("fixture-api-key", handler.GoogleApiKeyHeader);
        Assert.Collection(
            events,
            modelEvent => Assert.Equal(
                "Gemini stream response",
                Assert.IsType<TextDelta>(modelEvent).Text),
            modelEvent => Assert.IsType<ModelCompleted>(modelEvent));
    }

    private static ModelRequest CreateRequest()
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(
            providerId,
            "Gemini fixture",
            ProviderProtocol.GeminiGenerateContent,
            new Uri("https://example.test/v1beta/"),
            "fixture-api-key",
            null,
            true,
            true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), providerId, "gemini-3.8-flash", "Gemini fixture",
            128_000, 8_192, "high", null, null, false, false, true, true, "{}");
        return new ModelRequest(
            provider, model, [new ModelMessage(ModelRole.User, "hello")], []);
    }
}
