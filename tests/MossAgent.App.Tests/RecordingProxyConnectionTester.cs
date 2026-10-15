using MossAgent.Application.Networking;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class RecordingProxyConnectionTester : IProxyConnectionTester
{
    public List<ProxyProfile> Tested { get; } = [];
    public Func<CancellationToken, Task>? BeforeResult { get; set; }

    public async Task<ProxyConnectionTestResult> TestAsync(ProxyProfile proxy, CancellationToken cancellationToken)
    {
        Tested.Add(proxy);
        if (BeforeResult is not null) await BeforeResult(cancellationToken);
        return new ProxyConnectionTestResult(true, TimeSpan.FromMilliseconds(12), "Connected");
    }
}
