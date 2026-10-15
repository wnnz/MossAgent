using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MossAgent.App.ViewModels;

public sealed class AppStartupViewModel : ObservableObject
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.StartupStrings", typeof(AppStartupViewModel).Assembly);
    private bool _isLoading;
    private bool _hasWarning;

    public bool IsLoading => _isLoading;
    public bool IsReady => !_isLoading;
    public bool HasWarning => _hasWarning;
    public bool HasStatus => IsLoading || HasWarning;
    public string Status => Label(IsLoading ? "ConnectingMcp" : "McpStartupWarning");
    public string OpenSettingsLabel => Label("OpenMcpSettings");

    internal void Begin()
    {
        _hasWarning = false;
        _isLoading = true;
        NotifyStateChanged();
    }

    internal void Complete(bool hasWarning)
    {
        _hasWarning = hasWarning;
        _isLoading = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(Status));
    }

    private static string Label(string key) => Labels.GetString(key)
        ?? throw new InvalidOperationException($"Missing startup resource: {key}");
}
