using System.Net;

namespace MossAgent.Providers.Tests;

internal sealed class ProviderByteResponseHandler(byte[] bytes, HttpStatusCode status = HttpStatusCode.OK,
    bool declaredLength = false, bool blockAtEnd = false, Action? onResponse = null) : HttpMessageHandler
{
    public ChunkedProviderReadStream? ResponseStream { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ResponseStream = new ChunkedProviderReadStream(bytes, blockAtEnd: blockAtEnd);
        var content = new StreamContent(ResponseStream);
        if (declaredLength) content.Headers.ContentLength = bytes.Length;
        var response = new HttpResponseMessage(status) { Content = content };
        onResponse?.Invoke();
        return Task.FromResult(response);
    }
}
