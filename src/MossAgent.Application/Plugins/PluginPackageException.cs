namespace MossAgent.Application.Plugins;

/// <summary>Raised when a local plugin package is rejected. <see cref="Code"/> is a stable
/// <see cref="PluginPackageErrorCode"/> value; the message is internal diagnostics only.</summary>
public sealed class PluginPackageException(string code, string detail, Exception? innerException = null)
    : Exception($"Plugin package rejected ({code}): {detail}", innerException)
{
    public string Code { get; } = code;
}
