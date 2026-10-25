using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Anthropic;

public sealed class AnthropicMessagesProvider(IProviderHttpClientFactory clients) : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.AnthropicMessages;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ModelCapabilityGate.Check(request) is { } capabilityFailure)
        {
            yield return capabilityFailure;
            yield break;
        }
        var toolNames = new ProviderToolNameMap(request.Tools);
        using var client = await clients.CreateAsync(request.Provider, cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, "messages");
        message.Headers.TryAddWithoutValidation("x-api-key", request.Provider.ApiKey);
        message.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        message.Content = new StringContent(
            AnthropicRequestBuilder.Build(request, toolNames), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var parser = new AnthropicStreamParser(toolNames);
        await foreach (var frame in ProviderSseReader.ReadAsync(response, request.Provider, cancellationToken))
        {
            if (frame.Failure is { } streamFailure)
            {
                yield return streamFailure;
                yield break;
            }
            var data = frame.Data!;
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
            if (parser.Parse(document.RootElement) is { } modelEvent)
            {
                yield return ProviderErrorSanitizer.Sanitize(modelEvent, request.Provider);
                if (modelEvent is ModelFailed or ModelCompleted) yield break;
            }
        }
        yield return new ModelFailed("incomplete_response", ProviderText.Get("StreamIncompleteResponse"));
    }
}
