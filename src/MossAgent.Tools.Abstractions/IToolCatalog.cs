namespace MossAgent.Tools.Abstractions;

public interface IToolCatalog
{
    IReadOnlyCollection<ToolDescriptor> Descriptors { get; }
    IReadOnlyCollection<ToolDescriptor> GetModelDescriptors(ToolExecutionContext context) => Descriptors;
}

