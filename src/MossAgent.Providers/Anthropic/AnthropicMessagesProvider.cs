using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Anthropic;

public sealed class AnthropicMessagesProvider(
    IProviderHttpClientFactory clients,
    IModelDiscoveryService discovery) : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.AnthropicMessages;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var toolNames = new ProviderToolNameMap(request.Tools);
        using var client = await clients.CreateAsync(request.Provider, cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, "messages");
        message.Headers.TryAddWithoutValidation("x-api-key", request.Provider.ApiKey);
        message.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        message.Content = new StringContent(
            AnthropicRequestBuilder.Build(request, toolNames), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return new ModelFailed(
                "http_error",
                $"HTTP {(int)response.StatusCode}: {ProviderErrorSanitizer.Sanitize(error, request.Provider)}");
            yield break;
        }

        var parser = new AnthropicStreamParser(toolNames);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var data in SseDataReader.ReadAsync(stream, cancellationToken))
        {
            using var document = JsonDocument.Parse(data);
            if (parser.Parse(document.RootElement) is { } modelEvent)
            {
                yield return modelEvent;
            }
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        var models = await discovery.DiscoverAsync(provider, cancellationToken);
        return models.Select(static model => model.Id).ToArray();
    }

}
