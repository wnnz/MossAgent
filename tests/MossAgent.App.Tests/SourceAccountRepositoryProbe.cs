using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class SourceAccountRepositoryProbe(ISourceAccountRepository inner) : ISourceAccountRepository
{
    public Func<Task> AfterSaveAsync { get; set; } = () => Task.CompletedTask;
    public Func<Task> AfterGetAsync { get; set; } = () => Task.CompletedTask;
    public int SaveCalls { get; private set; }

    public Task<IReadOnlyList<SourceAccount>> GetAllAsync(CancellationToken cancellationToken) =>
        inner.GetAllAsync(cancellationToken);

    public async Task<SourceAccount?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await inner.GetAsync(id, cancellationToken);
        await AfterGetAsync();
        return account;
    }

    public async Task<SourceAccount> SaveAsync(SourceAccount account, CancellationToken cancellationToken)
    {
        SaveCalls++;
        var saved = await inner.SaveAsync(account, cancellationToken);
        await AfterSaveAsync();
        return saved;
    }

    public Task DeleteAsync(Guid id, long revision, CancellationToken cancellationToken) =>
        inner.DeleteAsync(id, revision, cancellationToken);
}
