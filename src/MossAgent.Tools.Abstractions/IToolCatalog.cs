namespace MossAgent.Tools.Abstractions;

public interface IToolCatalog
{
    IReadOnlyCollection<ToolDescriptor> Descriptors { get; }
}

