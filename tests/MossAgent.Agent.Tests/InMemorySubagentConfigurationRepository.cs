using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class InMemorySubagentConfigurationRepository(
    IEnumerable<SubagentProfile>? profiles = null) : ISubagentConfigurationRepository
{
    private readonly List<SubagentProfile> _profiles = profiles?.ToList() ?? [];

    public Task<IReadOnlyList<SubagentProfile>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SubagentProfile>>(_profiles.ToArray());

    public Task SaveAsync(SubagentProfile profile, CancellationToken cancellationToken)
    {
        _profiles.RemoveAll(item => item.Id == profile.Id);
        _profiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _profiles.RemoveAll(item => item.Id == id);
        return Task.CompletedTask;
    }
}
