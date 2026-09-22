using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

public sealed class OpenAiResponsesProvider(
    IProviderHttpClientFactory clients,
    IModelDiscoveryService discovery) : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var toolNames = new ProviderToolNameMap(request.Tools);
        using var client = await clients.CreateAsync(request.Provider, cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, "responses");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Provider.ApiKey);
        message.Content = new StringContent(
            OpenAiRequestBuilder.Build(request, toolNames), Encoding.UTF8, "application/json");
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
            if (data == "[DONE]")
            {
                yield break;
            }

            using var document = JsonDocument.Parse(data);
            if (OpenAiEventMapper.Map(document.RootElement, toolNames) is { } modelEvent)
            {
                yield return modelEvent;
            }
        }
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(
        AiProvider provider,
        CancellationToken cancellationToken) => DiscoverIdsAsync(provider, cancellationToken);

    private async Task<IReadOnlyList<string>> DiscoverIdsAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        var models = await discovery.DiscoverAsync(provider, cancellationToken);
        return models.Select(static model => model.Id).ToArray();
    }

}
