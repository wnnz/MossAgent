using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceInspectorViewModel : ObservableObject, IDisposable
{
    private const double ContextCardOverlapThreshold = 1420;
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly Dictionary<(Guid? ProjectId, Guid? TaskId), (string Tab, bool Open, bool ContextEnabled)> _states = [];
    private (Guid? ProjectId, Guid? TaskId) _context;
    private string _activeInspectorTab = "home";
    private bool _isRightPanelOpen;
    private bool _isContextCardEnabled = true;
    private double _contextCardHeight;
    private double _chatViewportWidth;
    private bool _disposed;

    public WorkspaceInspectorViewModel(WorkspaceCatalogViewModel catalog)
    {
        _catalog = catalog;
        _context = SelectedContext();
        catalog.SelectionChanged += HandleSelectionChanged;
        catalog.TaskManagement.TaskArchived += HandleTaskArchived;
    }

    public bool IsRightPanelOpen
    {
        get => _isRightPanelOpen;
        set
        {
            if (_disposed) return;
            var previous = Presentation;
            if (SetProperty(ref _isRightPanelOpen, value)) NotifyPresentationProperties(previous);
        }
    }

    public string ActiveInspectorTab
    {
        get => _activeInspectorTab;
        set
        {
            var previous = _activeInspectorTab;
            if (_disposed || !InspectorText.IsValidTab(value)
                || !SetProperty(ref _activeInspectorTab, value)) return;
            NotifyTabProperties(previous);
        }
    }

    public bool IsContextCardVisible => _isContextCardEnabled && !IsRightPanelOpen;
    public string RightPanelToggleLabel => SettingsText.Get(IsRightPanelOpen ? "HideRightPanel" : "ShowRightPanel");
    public bool IsInspectorHome => ActiveInspectorTab == "home";
    public string InspectorTitle => InspectorText.TabTitle(ActiveInspectorTab);
    public bool IsInspectorDiff => ActiveInspectorTab == "diff";
    public bool IsInspectorTerminal => ActiveInspectorTab == "terminal";
    public bool IsInspectorBrowser => ActiveInspectorTab == "browser";
    public bool IsInspectorAudit => ActiveInspectorTab == "audit";
    public bool IsInspectorSources => ActiveInspectorTab is "home" or "sources";
    public Thickness ChatMessageViewportMargin => IsContextCardVisible && _contextCardHeight > 0
        && _chatViewportWidth < ContextCardOverlapThreshold
        ? new Thickness(0, _contextCardHeight + 22, 0, 0) : new Thickness(0);

    public void ToggleRightPanel() => IsRightPanelOpen = !IsRightPanelOpen;

    public void ToggleContextCard()
    {
        if (_disposed) return;
        var previous = Presentation;
        if (_isRightPanelOpen)
        {
            _isRightPanelOpen = false;
            _isContextCardEnabled = true;
            OnPropertyChanged(nameof(IsRightPanelOpen));
        }
        else _isContextCardEnabled = !_isContextCardEnabled;
        NotifyPresentationProperties(previous);
    }

    internal void SetChatViewportWidth(double width) => SetLayoutDimension(ref _chatViewportWidth, width);

    internal void SetContextCardHeight(double height) => SetLayoutDimension(ref _contextCardHeight, height);

    private void SetLayoutDimension(ref double dimension, double value)
    {
        if (_disposed || !double.IsFinite(value) || value < 0 || Math.Abs(dimension - value) < 0.5) return;
        var previous = ChatMessageViewportMargin;
        dimension = value;
        if (previous != ChatMessageViewportMargin) OnPropertyChanged(nameof(ChatMessageViewportMargin));
    }

    private (bool Open, bool Context, Thickness Margin) Presentation =>
        (_isRightPanelOpen, IsContextCardVisible, ChatMessageViewportMargin);

    private (Guid? ProjectId, Guid? TaskId) SelectedContext() => _catalog.SelectedTask is { } task
        ? (task.ProjectId, task.Id) : (_catalog.SelectedProject?.Id, null);

    private void HandleSelectionChanged()
    {
        if (_disposed) return;
        var context = SelectedContext();
        if (_context == context) return;
        var previous = Presentation;
        var previousTab = _activeInspectorTab;
        _states[_context] = (_activeInspectorTab, _isRightPanelOpen, _isContextCardEnabled);
        _context = context;
        var state = _states.GetValueOrDefault(context, (Tab: "home", Open: false, ContextEnabled: true));
        _activeInspectorTab = state.Tab;
        _isRightPanelOpen = state.Open;
        _isContextCardEnabled = state.ContextEnabled;
        if (previousTab != _activeInspectorTab)
        {
            OnPropertyChanged(nameof(ActiveInspectorTab));
            NotifyTabProperties(previousTab);
        }
        if (previous.Open != _isRightPanelOpen) OnPropertyChanged(nameof(IsRightPanelOpen));
        NotifyPresentationProperties(previous);
    }

    private void HandleTaskArchived(AgentTask task) => _states.Remove((task.ProjectId, task.Id));

    private void NotifyTabProperties(string previousTab)
    {
        if (InspectorText.TabTitle(previousTab) != InspectorTitle) OnPropertyChanged(nameof(InspectorTitle));
        if ((previousTab == "home") != IsInspectorHome) OnPropertyChanged(nameof(IsInspectorHome));
        if ((previousTab == "diff") != IsInspectorDiff) OnPropertyChanged(nameof(IsInspectorDiff));
        if ((previousTab == "terminal") != IsInspectorTerminal) OnPropertyChanged(nameof(IsInspectorTerminal));
        if ((previousTab == "browser") != IsInspectorBrowser) OnPropertyChanged(nameof(IsInspectorBrowser));
        if ((previousTab == "audit") != IsInspectorAudit) OnPropertyChanged(nameof(IsInspectorAudit));
        if ((previousTab is "home" or "sources") != IsInspectorSources) OnPropertyChanged(nameof(IsInspectorSources));
    }

    private void NotifyPresentationProperties((bool Open, bool Context, Thickness Margin) previous)
    {
        if (previous.Context != IsContextCardVisible) OnPropertyChanged(nameof(IsContextCardVisible));
        if (previous.Open != _isRightPanelOpen) OnPropertyChanged(nameof(RightPanelToggleLabel));
        if (previous.Margin != ChatMessageViewportMargin) OnPropertyChanged(nameof(ChatMessageViewportMargin));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _catalog.SelectionChanged -= HandleSelectionChanged;
        _catalog.TaskManagement.TaskArchived -= HandleTaskArchived;
        _states.Clear();
    }
}
