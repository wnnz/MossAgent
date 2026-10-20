namespace MossAgent.Tools.Abstractions;

public interface IToolRequestAsyncPrevalidator
{
    Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken);
}
