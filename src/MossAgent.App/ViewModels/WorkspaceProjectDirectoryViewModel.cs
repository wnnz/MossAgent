using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectDirectoryViewModel : ObservableObject
{
    private bool _isPrimary;

    public WorkspaceProjectDirectoryViewModel(
        string path,
        bool isPrimary,
        Action<WorkspaceProjectDirectoryViewModel> setPrimary,
        Action<WorkspaceProjectDirectoryViewModel> remove)
    {
        Path = path;
        _isPrimary = isPrimary;
        SetPrimaryCommand = new RelayCommand(() => setPrimary(this), () => IsSecondary);
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Path { get; }

    public string DisplayName
    {
        get
        {
            var name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path));
            return string.IsNullOrWhiteSpace(name) ? Path : name;
        }
    }

    public bool IsPrimary
    {
        get => _isPrimary;
        private set
        {
            if (!SetProperty(ref _isPrimary, value)) return;
            OnPropertyChanged(nameof(IsSecondary));
            SetPrimaryCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsSecondary => !IsPrimary;

    public IRelayCommand SetPrimaryCommand { get; }

    public IRelayCommand RemoveCommand { get; }

    public void SetPrimaryState(bool value) => IsPrimary = value;
}
