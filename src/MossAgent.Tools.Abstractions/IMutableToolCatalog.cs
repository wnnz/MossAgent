namespace MossAgent.Tools.Abstractions;

public interface IMutableToolCatalog : IToolCatalog
{
    void RegisterOrReplace(IAgentTool tool);
    void RemoveByPrefix(string prefix);
}

