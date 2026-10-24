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

public sealed class OpenAiResponsesProvider(IProviderHttpClientFactory clients) : IModelProvider
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

        await foreach (var frame in ProviderSseReader.ReadAsync(response, request.Provider, cancellationToken))
        {
            if (frame.Failure is { } streamFailure)
            {
                yield return streamFailure;
                yield break;
            }
            var data = frame.Data!;
            if (data == "[DONE]")
            {
                break;
            }

            JsonDocument? document;
            try
            {
                document = JsonDocument.Parse(data);
            }
            catch (JsonException)
            {
                document = null;
            }

            if (document is null)
            {
                yield return new ModelFailed("invalid_response", ProviderText.Get("StreamInvalidResponse"));
                yield break;
            }

            using (document)
            if (OpenAiEventMapper.Map(document.RootElement, toolNames) is { } modelEvent)
            {
                yield return ProviderErrorSanitizer.Sanitize(modelEvent, request.Provider);
                if (modelEvent is ModelFailed or ModelCompleted) yield break;
            }
        }
        yield return new ModelFailed("incomplete_response", ProviderText.Get("StreamIncompleteResponse"));
    }
}
