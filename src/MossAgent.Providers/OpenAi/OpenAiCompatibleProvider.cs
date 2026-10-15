using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

public sealed class OpenAiCompatibleProvider(IProviderHttpClientFactory clients) : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiCompatible;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var toolNames = new ProviderToolNameMap(request.Tools);
        using var client = await clients.CreateAsync(request.Provider, cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Provider.ApiKey);
        message.Content = new StringContent(
            OpenAiCompatibleRequestBuilder.Build(request, toolNames), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var parser = new OpenAiCompatibleStreamParser(toolNames);
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
