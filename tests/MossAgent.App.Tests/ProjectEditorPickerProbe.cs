using MossAgent.App.Services;

namespace MossAgent.App.Tests;

internal sealed class ProjectEditorPickerProbe : IFolderPickerService
{
    public Queue<string?> Results { get; } = [];
    public int Calls { get; private set; }

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(Results.Dequeue());
    }
}
