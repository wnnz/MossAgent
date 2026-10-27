using MossAgent.App.Services;

namespace MossAgent.App.Tests;

/// <summary>Returns fixed package paths and proves the plugin picker entry is used
/// instead of the attachment pickers.</summary>
internal sealed class PluginPackagePickerProbe : IFilePickerService
{
    public int PackageCalls { get; private set; }
    public List<CancellationToken> Tokens { get; } = [];
    public IReadOnlyList<string> Paths { get; set; } = [];

    public Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Attachment picker must not be used.");

    public Task<IReadOnlyList<string>> PickPluginPackageAsync(CancellationToken cancellationToken)
    {
        PackageCalls++;
        Tokens.Add(cancellationToken);
        return Task.FromResult(Paths);
    }
}
