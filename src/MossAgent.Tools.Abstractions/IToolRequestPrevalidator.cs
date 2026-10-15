namespace MossAgent.Tools.Abstractions;

public interface IToolRequestPrevalidator
{
    void ValidateRequest(ToolRequest request, ToolExecutionContext context);
}
