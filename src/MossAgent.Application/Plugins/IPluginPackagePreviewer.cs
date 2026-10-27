using MossAgent.Domain.Plugins;

namespace MossAgent.Application.Plugins;

/// <summary>Reads a local plugin package and returns an immutable preview. Fail-closed:
/// every rejection throws <see cref="PluginPackageException"/> with a stable code and
/// cancellation propagates <see cref="OperationCanceledException"/> unwrapped.</summary>
public interface IPluginPackagePreviewer
{
    Task<PluginPackagePreview> PreviewAsync(string packagePath, CancellationToken cancellationToken);
}
