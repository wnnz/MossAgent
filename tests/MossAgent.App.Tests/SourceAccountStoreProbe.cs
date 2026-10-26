using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class SourceAccountStoreProbe : ISourceAccountRepository
{
    private readonly Dictionary<Guid, SourceAccount> _accounts = [];

    public int GetCalls { get; private set; }

    public SourceAccount Store(SourceAccount account)
    {
        var stored = account.WithRevision(account.Revision + 1);
        _accounts[account.Id] = stored;
        return stored;
    }

    public Task<IReadOnlyList<SourceAccount>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SourceAccount>>(_accounts.Values.OrderBy(account => account.Name).ToArray());

    public Task<SourceAccount?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        GetCalls++;
        return Task.FromResult(_accounts.GetValueOrDefault(id));
    }

    public Task<SourceAccount> SaveAsync(SourceAccount account, CancellationToken cancellationToken) =>
        Task.FromResult(Store(account));

    public Task DeleteAsync(Guid id, long revision, CancellationToken cancellationToken)
    {
        _accounts.Remove(id);
        return Task.CompletedTask;
    }
}
