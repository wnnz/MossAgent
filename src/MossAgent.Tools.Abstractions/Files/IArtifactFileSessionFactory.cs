namespace MossAgent.Tools.Abstractions.Files;

public interface IArtifactFileSessionFactory
{
    Task<IArtifactFileSession> CreateAsync(ToolExecutionContext context, string prefix,
        string extension, CancellationToken cancellationToken);
}
