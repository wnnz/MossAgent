namespace MossAgent.Application.Settings;

public interface IComposerPreferenceStore
{
    Task<ComposerRunningAction> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(ComposerRunningAction action, CancellationToken cancellationToken);
}
