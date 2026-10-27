using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.Tests;

/// <summary>Records previewed paths and returns a canned preview or failure.</summary>
internal sealed class RecordingPluginPackagePreviewer : IPluginPackagePreviewer
{
    public List<string> Paths { get; } = [];
    public PluginPackagePreview? Result { get; set; }
    public Exception? Failure { get; set; }
    public Func<string, PluginPackagePreview>? Handler { get; set; }

    public Task<PluginPackagePreview> PreviewAsync(string packagePath, CancellationToken cancellationToken)
    {
        Paths.Add(packagePath);
        if (Failure is not null) return Task.FromException<PluginPackagePreview>(Failure);
        return Task.FromResult(Handler?.Invoke(packagePath) ?? Result ?? throw new NotSupportedException());
    }
}
