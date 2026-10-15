using System.Net;
using System.Text;

namespace MossAgent.Providers.Tests;

public sealed class PaginatedModelResponseHandler(params string[] contents) : HttpMessageHandler
{
    private readonly Queue<string> _contents = new(contents);
    private readonly List<Uri> _requestUris = [];

    public IReadOnlyList<Uri> RequestUris => _requestUris;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestUri is { } requestUri)
        {
            _requestUris.Add(requestUri);
        }

        if (!_contents.TryDequeue(out var content))
        {
            return Task.FromException<HttpResponseMessage>(
                new InvalidOperationException("No model response fixture remains."));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });
    }
}
