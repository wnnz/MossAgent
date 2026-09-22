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
        _tools.Values.Select(static tool => tool.Descriptor).ToArray();

    public bool TryGet(string name, out IAgentTool? tool) => _tools.TryGetValue(name, out tool);

    public void RegisterOrReplace(IAgentTool tool) => _tools[tool.Descriptor.Name] = tool;

    public void RemoveByPrefix(string prefix)
    {
        foreach (var name in _tools.Keys.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            _tools.TryRemove(name, out _);
        }
    }
}
