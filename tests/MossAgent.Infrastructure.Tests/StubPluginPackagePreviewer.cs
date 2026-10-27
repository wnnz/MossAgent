using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Returns a canned preview regardless of the archive on disk, standing in for a
/// caller-supplied preview the installer must never trust.</summary>
internal sealed class StubPluginPackagePreviewer(PluginPackagePreview preview) : IPluginPackagePreviewer
{
    public Task<PluginPackagePreview> PreviewAsync(string packagePath, CancellationToken cancellationToken) =>
        Task.FromResult(preview);
}
