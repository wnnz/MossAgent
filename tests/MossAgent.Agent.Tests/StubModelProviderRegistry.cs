using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class StubModelProviderRegistry(IModelProvider provider) : IModelProviderRegistry
{
    public IModelProvider Get(ProviderProtocol protocol) =>
        provider.Protocol == protocol
            ? provider
            : throw new NotSupportedException($"Unsupported protocol: {protocol}");
}
