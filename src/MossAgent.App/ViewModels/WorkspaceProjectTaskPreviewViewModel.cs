using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectTaskPreviewViewModel : ObservableObject
{
    private const int PreviewCount = 5;
    private readonly List<WorkspaceTaskNavigationViewModel> _preview = new(PreviewCount);
    private IReadOnlyList<WorkspaceTaskNavigationViewModel> _rows = [];
    private bool _isFiltering;
    private bool _isShowingAll;
    private bool _canToggle;
    private Guid? _pendingReveal;

    public WorkspaceProjectTaskPreviewViewModel()
    {
        ToggleCommand = new RelayCommand(Toggle, () => CanToggle);
    }

    public ObservableCollection<WorkspaceTaskNavigationViewModel> Items { get; } = [];
    public IRelayCommand ToggleCommand { get; }
    public bool IsShowingAll => _isShowingAll;
    public bool CanToggle => _canToggle;
    public string ToggleLabel => SidebarText.Get(IsShowingAll ? "CollapseTasksLabel" : "ExpandTasksLabel");

    internal void Update(IReadOnlyList<WorkspaceTaskNavigationViewModel> rows, bool isFiltering)
    {
        _rows = rows;
        _isFiltering = isFiltering;
        if (SetProperty(ref _canToggle, !isFiltering && rows.Count > PreviewCount, nameof(CanToggle)))
            ToggleCommand.NotifyCanExecuteChanged();
        Refresh();
        if (_pendingReveal is not null) Reveal(_pendingReveal);
    }

    internal void Reveal(Guid? taskId)
    {
        _pendingReveal = taskId;
        if (taskId is null || IsShowingAll)
        {
            _pendingReveal = null;
            return;
        }
        if (_isFiltering) return;
        for (var index = 0; index < _rows.Count; index++)
        {
            if (_rows[index].Task.Id != taskId) continue;
            _pendingReveal = null;
            if (index >= PreviewCount) SetShowingAll(true);
            return;
        }
    }

    private void Toggle()
    {
        if (!CanToggle) return;
        _pendingReveal = null;
        SetShowingAll(!IsShowingAll);
    }

    private void SetShowingAll(bool value)
    {
        if (!SetProperty(ref _isShowingAll, value, nameof(IsShowingAll))) return;
        OnPropertyChanged(nameof(ToggleLabel));
        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<WorkspaceTaskNavigationViewModel> visible = _rows;
        if (!_isFiltering && !IsShowingAll && _rows.Count > PreviewCount)
        {
            _preview.Clear();
            for (var index = 0; index < PreviewCount; index++) _preview.Add(_rows[index]);
            visible = _preview;
        }
        Items.SynchronizeWith(visible);
    }
}
