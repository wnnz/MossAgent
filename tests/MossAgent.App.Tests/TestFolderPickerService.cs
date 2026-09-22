using MossAgent.App.Services;

namespace MossAgent.App.Tests;

internal sealed class TestFolderPickerService(params string?[] selectedPaths) : IFolderPickerService
{
    private readonly Queue<string?> _selectedPaths = new(selectedPaths);

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_selectedPaths.TryDequeue(out var path) ? path : null);
}
