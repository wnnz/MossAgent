namespace MossAgent.Testing;

internal sealed class SourceAccountProbeHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respondAsync,
    Action onDispose) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        respondAsync(request, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing) onDispose();
        base.Dispose(disposing);
    }
}
