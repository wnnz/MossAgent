using System.Runtime.CompilerServices;
using System.Text;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers.Streaming;

internal static class ProviderSseReader
{
    public static async IAsyncEnumerable<SseDataFrame> ReadAsync(
        HttpResponseMessage response, AiProvider provider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var failure = await ProviderHttpErrorReader.ReadAsync(
                response.Content, response.StatusCode, provider, cancellationToken);
            yield return new SseDataFrame(null, failure);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var frame in ReadAsync(stream, cancellationToken))
            yield return frame;
    }

    public static async IAsyncEnumerable<SseDataFrame> ReadAsync(
        Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var events = SseDataReader.ReadAsync(stream, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            var hasNext = false;
            ModelFailed? failure = null;
            try
            {
                hasNext = await events.MoveNextAsync();
            }
            catch (SseInputLimitException exception)
            {
                failure = new ModelFailed(exception.Code, exception.Message);
            }
            catch (DecoderFallbackException)
            {
                failure = new ModelFailed("invalid_stream_encoding", ProviderText.Get("StreamInvalidEncoding"));
            }
            if (failure is not null)
            {
                yield return new SseDataFrame(null, failure);
                yield break;
            }
            if (!hasNext) yield break;
            yield return new SseDataFrame(events.Current, null);
        }
    }
}
