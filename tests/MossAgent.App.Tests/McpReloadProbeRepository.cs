using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class McpReloadProbeRepository : IMcpConfigurationRepository
{
    public IReadOnlyList<McpServerProfile> Items { get; set; } = [];
    public Func<CancellationToken, Task<IReadOnlyList<McpServerProfile>>>? Reader { get; set; }
    public int ReadCalls { get; private set; }
    public int WriteCalls { get; private set; }
    public CancellationToken ReadToken { get; private set; }
    public Func<CancellationToken, Task>? BeforeSave { get; set; }
    public Func<CancellationToken, Task>? BeforeDelete { get; set; }
    public CancellationToken WriteToken { get; private set; }

    public Task<IReadOnlyList<McpServerProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        ReadCalls++;
        ReadToken = cancellationToken;
        return Reader?.Invoke(cancellationToken) ?? Task.FromResult(Items);
    }

    public async Task<McpServerProfile> SaveAsync(McpServerProfile profile, CancellationToken cancellationToken)
    {
        WriteCalls++;
        WriteToken = cancellationToken;
        if (BeforeSave is not null) await BeforeSave(cancellationToken);
        var stored = profile with { Revision = (Items.FirstOrDefault(item => item.Id == profile.Id)?.Revision ?? 0) + 1 };
        Items = Items.Where(item => item.Id != profile.Id).Append(stored).ToArray();
        return stored;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        WriteCalls++;
        WriteToken = cancellationToken;
        if (BeforeDelete is not null) await BeforeDelete(cancellationToken);
        Items = Items.Where(item => item.Id != id).ToArray();
    }
}
