using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Testing;

internal sealed class SourceAccountHttpProbe : ISourceAccountHttpClientFactory
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> RespondAsync { get; set; } =
        (_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":42,\"login\":\"fixture-user\"}")
        });
    public int RequestCount { get; private set; }
    public int DisposedHandlers { get; private set; }
    public Guid? SelectedProxyId { get; private set; }

    public Task<HttpClient> CreateAsync(SourceAccount account, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectedProxyId = account.ProxyId;
        var handler = new SourceAccountProbeHandler(async (request, token) =>
        {
            RequestCount++;
            return await RespondAsync(request, token);
        }, () => DisposedHandlers++);
        return Task.FromResult(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
    }
}
