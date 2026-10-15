using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class DescriptorProbeToolCatalog(IReadOnlyCollection<ToolDescriptor> snapshot) : IToolCatalog
{
    public IReadOnlyCollection<ToolDescriptor> Snapshot { get; set; } = snapshot;
    public int Reads { get; private set; }

    public IReadOnlyCollection<ToolDescriptor> Descriptors
    {
        get
        {
            Reads++;
            return Snapshot;
        }
    }
}
