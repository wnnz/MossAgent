using MossAgent.App.Services;

namespace MossAgent.App.Tests;

internal sealed class TestFolderPickerService(string? selectedPath) : IFolderPickerService
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
        Task.FromResult(selectedPath);
}
