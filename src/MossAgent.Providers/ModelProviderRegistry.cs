using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers;

public sealed class ModelProviderRegistry(IEnumerable<IModelProvider> providers)
    : IModelProviderRegistry
{
    private readonly IReadOnlyDictionary<ProviderProtocol, IModelProvider> _providers =
        providers.ToDictionary(static provider => provider.Protocol);

    public IModelProvider Get(ProviderProtocol protocol)
    {
        return _providers.TryGetValue(protocol, out var provider)
            ? provider
            : throw new NotSupportedException($"尚未实现 {protocol} 的对话协议。");
    }
}

