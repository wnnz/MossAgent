using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Gemini;

public sealed class GeminiGenerateContentProvider(
    IProviderHttpClientFactory clients,
    IModelDiscoveryService discovery) : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.GeminiGenerateContent;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var toolNames = new ProviderToolNameMap(request.Tools);
        using var client = await clients.CreateAsync(request.Provider, cancellationToken);
        var model = Uri.EscapeDataString(request.Model.ModelId);
        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"models/{model}:streamGenerateContent?alt=sse");
        message.Headers.TryAddWithoutValidation("x-goog-api-key", request.Provider.ApiKey);
        message.Content = new StringContent(
            GeminiRequestBuilder.Build(request, toolNames), Encoding.UTF8, "application/json");
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

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var data in SseDataReader.ReadAsync(stream, cancellationToken))
        {
            using var document = JsonDocument.Parse(data);
            foreach (var modelEvent in GeminiEventMapper.Map(document.RootElement, toolNames))
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
