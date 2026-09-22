using MossAgent.Domain;

namespace MossAgent.Application.Networking;

public interface IProxyConnectionTester
{
    Task<ProxyConnectionTestResult> TestAsync(
        ProxyProfile proxy,
        CancellationToken cancellationToken);
}
