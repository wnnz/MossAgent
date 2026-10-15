using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class TestAgentConfigurationRepository : IAgentConfigurationRepository
{
    private readonly List<AgentProfile> _profiles = [];

    public IReadOnlyList<AgentProfile> Profiles => _profiles;
    public Func<CancellationToken, Task>? BeforeSave { get; set; }
    public Func<CancellationToken, Task>? BeforeDelete { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<AgentProfile>>>? Loader { get; set; }
    public int SaveCalls { get; private set; }
    public int ReadCalls { get; private set; }
    public CancellationToken ReadToken { get; private set; }
    public int DeleteCalls { get; private set; }
    public CancellationToken SaveToken { get; private set; }
    public CancellationToken DeleteToken { get; private set; }

    public Task<IReadOnlyList<AgentProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        ReadCalls++;
        ReadToken = cancellationToken;
        return Loader?.Invoke(cancellationToken) ?? Task.FromResult<IReadOnlyList<AgentProfile>>(_profiles.ToArray());
    }

    public async Task SaveAsync(AgentProfile profile, CancellationToken cancellationToken)
    {
        SaveCalls++;
        SaveToken = cancellationToken;
        if (BeforeSave is { } wait) await wait(cancellationToken);
        _profiles.RemoveAll(item => item.Id == profile.Id);
        _profiles.Add(profile);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        DeleteCalls++;
        DeleteToken = cancellationToken;
        if (BeforeDelete is { } wait) await wait(cancellationToken);
        _profiles.RemoveAll(item => item.Id == id);
    }
}
