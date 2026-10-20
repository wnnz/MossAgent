using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Execution;

using System.Collections.Concurrent;

public sealed class ToolRegistry(IEnumerable<IAgentTool> tools) : IMutableToolCatalog
{
    private readonly ConcurrentDictionary<string, IAgentTool> _tools = new(
        tools.ToDictionary(
        static tool => tool.Descriptor.Name,
        StringComparer.OrdinalIgnoreCase),
        StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ToolDescriptor> Descriptors =>
        _tools.Values.Select(static tool => tool.Descriptor).Where(static descriptor => descriptor.IsModelVisible).ToArray();

    public bool TryGet(string name, out IAgentTool? tool) => _tools.TryGetValue(name, out tool);

    public IReadOnlyCollection<ToolDescriptor> GetModelDescriptors(ToolExecutionContext context) =>
        context.SourceReviewRevision is null ? Descriptors : context.SourceScope is null ? []
        : _tools.Values.Select(static tool => tool.Descriptor)
            .Where(descriptor => SourceReviewToolPolicy.IsAllowed(descriptor.Name) && context.IsToolAllowed(descriptor.Name)
                && descriptor.RiskLevel == ToolRiskLevel.ReadOnly && descriptor.RequiredCapabilities == ToolCapability.Network)
            .Select(static descriptor => descriptor with { IsModelVisible = true }).ToArray();

    public void RegisterOrReplace(IAgentTool tool) => _tools[tool.Descriptor.Name] = tool;

    public void RemoveByPrefix(string prefix)
    {
        foreach (var name in _tools.Keys.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            _tools.TryRemove(name, out _);
        }
    }
}
