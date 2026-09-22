using System.Collections.ObjectModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunState(
    AgentTask task,
    ProjectProfile project,
    AiProvider provider,
    ModelProfile model,
    ObservableCollection<ChatMessageViewModel> messages,
    ChatMessageViewModel assistant) : IDisposable
{
    private readonly Lock _cancellationLock = new();
    private CancellationTokenSource? _cancellation = new();

    public AgentTask Task { get; set; } = task;
    public ProjectProfile Project { get; } = project;
    public AiProvider Provider { get; } = provider;
    public ModelProfile Model { get; } = model;
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = messages;
    public ChatMessageViewModel Assistant { get; } = assistant;
    public CancellationToken CancellationToken
    {
        get
        {
            lock (_cancellationLock)
            {
                return _cancellation?.Token ?? CancellationToken.None;
            }
        }
    }

    public bool IsCancellationRequested
    {
        get
        {
            lock (_cancellationLock)
            {
                return _cancellation?.IsCancellationRequested == true;
            }
        }
    }

    public string Activity { get; set; } = "准备运行…";

    public void Cancel()
    {
        lock (_cancellationLock)
        {
            _cancellation?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_cancellationLock)
        {
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }
}
