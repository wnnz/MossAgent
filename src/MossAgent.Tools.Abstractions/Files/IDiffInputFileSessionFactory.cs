namespace MossAgent.Tools.Abstractions.Files;

public interface IDiffInputFileSessionFactory
{
    const int MaximumBytes = 4 * 1024 * 1024;

    Task<IDiffInputFileSession> CreateAsync(ToolExecutionContext context,
        ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
