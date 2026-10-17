using System.Collections.ObjectModel;
using MossAgent.App.Resources;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class McpSettingsCatalog(
    IMcpConfigurationRepository configurations, IConfigurationRepository proxies) : IDisposable
{
    private int _generation;
    private bool _disposed;

    public ObservableCollection<McpServerProfile> Items { get; } = [];
    public ObservableCollection<ProxySelection> ProxyOptions { get; } = [];
    public bool IsPublishing { get; private set; }
    public int Generation => _generation;

    public void Invalidate() => _generation++;

    public async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return false;
        var generation = ++_generation;
        var servers = await configurations.GetAllAsync(cancellationToken);
        var proxyProfiles = await proxies.GetProxiesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || generation != _generation) return false;
        IsPublishing = true;
        try
        {
            if (!Items.SequenceEqual(servers)) Items.ReplaceWith(servers);
            ProxyOptions.SynchronizeWith(ProxySelection.FromProfiles(proxyProfiles, SettingsText.Get("McpDirect")).ToArray());
        }
        finally { IsPublishing = false; }
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        Invalidate();
    }
}
