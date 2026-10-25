using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Tools.Tests;

internal sealed class McpShutdownConfigurationRepository : IMcpConfigurationRepository
{
    public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<IReadOnlyList<McpServerProfile>> ReadReleased { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken ReadToken { get; private set; }
    public int ReadCount { get; private set; }

    public Task<IReadOnlyList<McpServerProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        ReadToken = cancellationToken;
        ReadCount++;
        ReadStarted.TrySetResult();
        return ReadReleased.Task;
    }

    public Task<McpServerProfile> SaveAsync(McpServerProfile profile, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
