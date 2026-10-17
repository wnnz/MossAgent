using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;

namespace MossAgent.App.Tests;

internal sealed class SettingsHeaderPageFixture : IDisposable
{
    private readonly List<IDisposable> _owners = [];

    public SettingsHeaderPageFixture(string page)
    {
        var repository = new ModelReadProbeConfigurationRepository([], []);
        switch (page)
        {
            case "providers":
                var providers = new ProviderSettingsViewModel(repository);
                var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
                _owners.Add(providers);
                _owners.Add(models);
                View = new ProviderAndModelSettingsPane { DataContext = new { Providers = providers, Models = models } };
                CreateCommand = providers.OpenCreateFormCommand;
                RefreshCommand = providers.RefreshCommand;
                CloseCommand = providers.CloseFormCommand;
                IsEditorOpen = () => providers.IsFormOpen;
                break;
            case "proxy":
                var proxy = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
                _owners.Add(proxy);
                View = new ProxySettingsPane { DataContext = proxy };
                CreateCommand = proxy.OpenCreateFormCommand;
                RefreshCommand = proxy.RefreshCommand;
                CloseCommand = proxy.CloseFormCommand;
                IsEditorOpen = () => proxy.IsFormOpen;
                break;
            case "mcp":
                var mcp = new McpSettingsReloadFixture();
                _owners.Add(mcp);
                View = new McpSettingsPane { DataContext = mcp.Model };
                CreateCommand = mcp.Model.OpenCreateFormCommand;
                RefreshCommand = mcp.Model.ReloadCommand;
                CloseCommand = mcp.Model.CloseFormCommand;
                IsEditorOpen = () => mcp.Model.IsFormOpen;
                break;
            case "agent":
                var agent = new AgentEditorTestFixture();
                _owners.Add(agent);
                View = new AgentSettingsPane { DataContext = agent.Settings };
                CreateCommand = agent.Settings.OpenCreateFormCommand;
                RefreshCommand = agent.Settings.RefreshCommand;
                CloseCommand = agent.Settings.CloseFormCommand;
                IsEditorOpen = () => agent.Settings.IsFormOpen;
                break;
            default:
                var child = new SubagentEditorTestFixture();
                _owners.Add(child);
                View = new SubagentSettingsPane { DataContext = child.Settings };
                CreateCommand = child.Settings.OpenCreateFormCommand;
                RefreshCommand = child.Settings.RefreshCommand;
                CloseCommand = child.Settings.CloseFormCommand;
                IsEditorOpen = () => child.Settings.IsFormOpen;
                break;
        }
    }

    public AppUserControl View { get; }
    public ICommand CreateCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public ICommand CloseCommand { get; }
    public Func<bool> IsEditorOpen { get; }

    public void Dispose()
    {
        for (var index = _owners.Count - 1; index >= 0; index--) _owners[index].Dispose();
    }
}
