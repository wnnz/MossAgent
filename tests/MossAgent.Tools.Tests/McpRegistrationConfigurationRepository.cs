using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Tools.Tests;

internal sealed class McpRegistrationConfigurationRepository : IMcpConfigurationRepository
{
    public List<McpServerProfile> Profiles { get; } = [];

    public Task<IReadOnlyList<McpServerProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<McpServerProfile>>(Profiles.ToArray());
    }

    public Task<McpServerProfile> SaveAsync(McpServerProfile profile, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
