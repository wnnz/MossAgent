using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Routes Enter and Ctrl+Enter to the selected composer action while run availability remains owned
/// by the workspace.
/// </summary>
public sealed class WorkspaceComposerRouter : ObservableObject, IDisposable
{
    private readonly ComposerPreferenceViewModel _preferences;
    private readonly IRelayCommand _defaultCommand;
    private readonly IRelayCommand _alternateCommand;
    private readonly Func<bool> _canAct;

    public WorkspaceComposerRouter(ComposerPreferenceViewModel preferences, IRelayCommand defaultCommand,
        IRelayCommand alternateCommand, Func<bool> canAct)
    {
        _preferences = preferences;
        _defaultCommand = defaultCommand;
        _alternateCommand = alternateCommand;
        _canAct = canAct;
        _preferences.PropertyChanged += HandlePreferenceChanged;
    }

    public bool SubmitOnEnter => _preferences.SubmitOnEnter;
    public string Hint => _preferences.Hint;
    public IRelayCommand SubmitCommand => _defaultCommand;
    public IRelayCommand AlternateSubmitCommand => SubmitOnEnter ? _alternateCommand : _defaultCommand;
    public bool CanUseShortcuts => _preferences.IsReady && _canAct();

    public void UpdateAvailability() => OnPropertyChanged(nameof(CanUseShortcuts));

    private void HandlePreferenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(ComposerPreferenceViewModel.IsReady)
            or nameof(ComposerPreferenceViewModel.SelectedIndex))) return;
        OnPropertyChanged(nameof(SubmitOnEnter));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(AlternateSubmitCommand));
        OnPropertyChanged(nameof(CanUseShortcuts));
    }

    public void Dispose()
    {
        _preferences.PropertyChanged -= HandlePreferenceChanged;
    }
}
