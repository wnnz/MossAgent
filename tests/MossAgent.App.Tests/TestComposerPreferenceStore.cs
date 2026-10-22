using MossAgent.Application.Settings;

namespace MossAgent.App.Tests;

internal sealed class TestComposerPreferenceStore : IComposerPreferenceStore
{
    public ComposerSendMode Mode { get; set; } = ComposerSendMode.EnterSends;
    public bool ThrowOnLoad { get; set; }
    public bool ThrowOnSave { get; set; }
    public int SaveCount { get; private set; }

    public Task<ComposerSendMode> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ThrowOnLoad
            ? Task.FromException<ComposerSendMode>(new InvalidDataException("synthetic load failure"))
            : Task.FromResult(Mode);
    }

    public Task SaveAsync(ComposerSendMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnSave) return Task.FromException(new IOException("synthetic save failure"));
        Mode = mode;
        SaveCount++;
        return Task.CompletedTask;
    }
}
