using MossAgent.App.Services;

namespace MossAgent.App.Tests;

internal sealed class DeferredFolderPickerService : IFolderPickerService
{
    private readonly TaskCompletionSource<string?> _selection =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
        _selection.Task.WaitAsync(cancellationToken);

    public void Complete(string? path) => _selection.SetResult(path);
}
