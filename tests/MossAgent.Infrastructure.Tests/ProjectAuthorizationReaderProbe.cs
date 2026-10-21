using MossAgent.Application.Authorization;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Tests;

internal sealed class ProjectAuthorizationReaderProbe(ProjectProfile project) : IProjectAuthorizationReader
{
    private ProjectProfile? _current = project;
    private int _reads;
    public ProjectProfile? Current { get => Volatile.Read(ref _current); set => Volatile.Write(ref _current, value); }
    public Func<int, CancellationToken, Task<ProjectProfile?>>? Read { get; set; }
    public int Reads => Volatile.Read(ref _reads);

    public Task<ProjectProfile?> ReadAuthorizationAsync(Guid projectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = Interlocked.Increment(ref _reads);
        return Read?.Invoke(count, cancellationToken) ?? Task.FromResult(Current?.Id == projectId ? Current : null);
    }
}
