using System.Runtime.CompilerServices;
using System.Text;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Gemini;

public sealed class GeminiGenerateContentProvider(IProviderHttpClientFactory clients) : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.GeminiGenerateContent;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ModelReasoningPolicy.Supports(Protocol, request.Model.ModelId, request.Model.ReasoningEffort))
        {
            yield return new ModelFailed("unsupported_reasoning_effort",
                "This Gemini model does not support the selected reasoning effort. Choose a supported effort in model settings.");
            yield break;
        }
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
        var parser = new GeminiStreamParser(toolNames, request);
        await foreach (var frame in ProviderSseReader.ReadAsync(response, request.Provider, cancellationToken))
        {
            if (frame.Failure is { } streamFailure)
            {
                yield return streamFailure;
                yield break;
            }
            var data = frame.Data!;
            foreach (var modelEvent in parser.Parse(data))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return ProviderErrorSanitizer.Sanitize(modelEvent, request.Provider);
            }
            if (parser.IsTerminal) yield break;
        }
        foreach (var modelEvent in parser.Complete())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ProviderErrorSanitizer.Sanitize(modelEvent, request.Provider);
        }
    }
}
