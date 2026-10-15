using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Controls;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class EmbeddedBrowserWorkspaceViewModel : ObservableObject
{
    private readonly Dictionary<Guid, EmbeddedBrowserPaneViewModel> _panes = [];
    private readonly EmbeddedBrowserPaneViewModel _emptyPane;
    private EmbeddedBrowserPaneViewModel _currentPane;
    private Guid? _selectedTask;
    private ToolExecutionContext? _selectedContext;
    private readonly IToolExecutor? _executor;

    public EmbeddedBrowserWorkspaceViewModel(IToolExecutor? executor = null)
    {
        _executor = executor;
        Host = new AppBrowserHost();
        _emptyPane = new EmbeddedBrowserPaneViewModel(Host);
        _currentPane = _emptyPane;
    }

    public AppBrowserHost Host { get; }
    public EmbeddedBrowserPaneViewModel CurrentPane
    {
        get => _currentPane;
        private set => SetProperty(ref _currentPane, value);
    }

    public void SelectTask(Guid? taskId, ToolExecutionContext? context = null)
    {
        if (context is not null && context.TaskId != taskId)
            throw new ArgumentException("The selected browser context must belong to its task.", nameof(context));
        if (_selectedTask != taskId) CurrentPane.AttachContext(null);
        _selectedTask = taskId;
        _selectedContext = context;
        CurrentPane = taskId is { } id && _panes.TryGetValue(id, out var pane) ? pane : _emptyPane;
        CurrentPane.AttachContext(ReferenceEquals(CurrentPane, _emptyPane) ? null : context);
    }

    internal EmbeddedBrowserPaneViewModel Open(Guid taskId)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("A browser requires an owning task.", nameof(taskId));
        if (_panes.ContainsKey(taskId)) throw new InvalidOperationException("The task already owns a browser pane.");
        var pane = new EmbeddedBrowserPaneViewModel(Host, _executor, taskId);
        _panes.Add(taskId, pane);
        if (_selectedTask == taskId)
        {
            CurrentPane = pane;
            pane.AttachContext(_selectedContext);
        }
        return pane;
    }

    internal void Close(Guid taskId, EmbeddedBrowserPaneViewModel pane)
    {
        if (!_panes.TryGetValue(taskId, out var current) || !ReferenceEquals(current, pane)) return;
        _panes.Remove(taskId);
        pane.Dispose();
        if (ReferenceEquals(CurrentPane, pane)) CurrentPane = _emptyPane;
    }
}
