using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace MossAgent.Providers.Tests;

public sealed class RecordingResponseHandler(
    HttpStatusCode statusCode,
    string content,
    Exception? sendError = null,
    string contentType = "application/json",
    bool unknownLength = false,
    Encoding? responseEncoding = null)
    : HttpMessageHandler
{
    public HttpMethod? RequestMethod { get; private set; }
    public Uri? RequestUri { get; private set; }
    public string? AuthorizationHeader { get; private set; }
    public string? GoogleApiKeyHeader { get; private set; }
    public string? AnthropicApiKeyHeader { get; private set; }
    public string? AnthropicVersionHeader { get; private set; }
    public string? RequestBody { get; private set; }
    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestCount++;
        if (sendError is not null)
        {
            throw sendError;
        }

        RequestMethod = request.Method;
        RequestUri = request.RequestUri;
        AuthorizationHeader = request.Headers.Authorization?.ToString();
        GoogleApiKeyHeader = ReadHeader(request.Headers, "x-goog-api-key");
        AnthropicApiKeyHeader = ReadHeader(request.Headers, "x-api-key");
        AnthropicVersionHeader = ReadHeader(request.Headers, "anthropic-version");
        RequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var response = new HttpResponseMessage(statusCode)
        {
            Content = unknownLength
                ? new UnknownLengthResponseContent(content)
                : new StringContent(content, responseEncoding ?? Encoding.UTF8, contentType)
        };
        return response;
    }

    private static string? ReadHeader(HttpRequestHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
