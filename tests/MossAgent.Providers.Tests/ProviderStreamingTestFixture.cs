using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.OpenAi;

namespace MossAgent.Providers.Tests;

internal static class ProviderStreamingTestFixture
{
    public static IModelProvider CreateProvider(ProviderProtocol protocol, HttpMessageHandler handler)
    {
        var clients = new StaticProviderHttpClientFactory(handler);
        return protocol switch
        {
            ProviderProtocol.OpenAiResponses => new OpenAiResponsesProvider(clients),
            ProviderProtocol.OpenAiCompatible => new OpenAiCompatibleProvider(clients),
            ProviderProtocol.AnthropicMessages => new AnthropicMessagesProvider(clients),
            ProviderProtocol.GeminiGenerateContent => new GeminiGenerateContentProvider(clients),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol))
        };
    }

    public static ModelRequest CreateRequest(ProviderProtocol protocol,
        string apiKey = "fixture-api-secret", IReadOnlyDictionary<string, string>? headers = null)
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(providerId, "fixture", protocol,
            new Uri("https://example.test/v1/"), apiKey, null, true, true,
            headers ?? new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), providerId, "fixture-model", "Fixture model",
            8192, 1024, "off", null, null, false, false, true, true, "{}");
        return new ModelRequest(provider, model, [new ModelMessage(ModelRole.User, "hello")], []);
    }

    public static async Task<List<ModelEvent>> CollectAsync(ProviderProtocol protocol,
        HttpMessageHandler handler, CancellationToken cancellationToken)
    {
        var events = new List<ModelEvent>();
        await foreach (var item in CreateProvider(protocol, handler).StreamAsync(
                           CreateRequest(protocol), cancellationToken))
            events.Add(item);
        return events;
    }
}
