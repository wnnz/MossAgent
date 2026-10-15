using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record AgentModelOption(ModelProfile Model, string ProviderName, ProviderProtocol? Protocol = null)
{
    public string Label => $"{ProviderName} · {Model.DisplayName} ({Model.ModelId})";
}
