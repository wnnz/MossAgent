using MossAgent.Application.Settings;

namespace MossAgent.App.Tests;

internal sealed class ComposerPreferenceStoreProbe : IComposerPreferenceStore
{
    public ComposerRunningAction Current { get; set; } = ComposerRunningAction.Queue;
    public Func<CancellationToken, Task<ComposerRunningAction>>? Read { get; set; }
    public Func<ComposerRunningAction, CancellationToken, Task>? Write { get; set; }
    public int Saves { get; private set; }
    public Task<ComposerRunningAction> LoadAsync(CancellationToken cancellationToken) =>
        Read?.Invoke(cancellationToken) ?? Task.FromResult(Current);
    public Task SaveAsync(ComposerRunningAction action, CancellationToken cancellationToken)
    {
        Saves++;
        return Write?.Invoke(action, cancellationToken) ?? Task.FromResult<ComposerRunningAction>(action).ContinueWith(_ => { }, cancellationToken);
    }
}
