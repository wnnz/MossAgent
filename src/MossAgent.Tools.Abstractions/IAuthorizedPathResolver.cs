namespace MossAgent.Tools.Abstractions;

public interface IAuthorizedPathResolver
{
    string Resolve(string path, ToolExecutionContext context);
}
