namespace MossAgent.Application.Settings;

public interface IComposerPreferenceStore
{
    Task<ComposerSendMode> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(ComposerSendMode mode, CancellationToken cancellationToken);
}
