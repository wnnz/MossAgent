using System.Collections.ObjectModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunState(
    AgentTask task,
    ProjectProfile? project,
    AiProvider provider,
    ModelProfile model,
    ObservableCollection<ChatMessageViewModel> messages,
    ChatMessageViewModel assistant,
    string? attachmentContext = null,
    string? agentInstructions = null) : IDisposable
{
    private readonly Lock _cancellationLock = new();
    private CancellationTokenSource? _cancellation = new();
    private CancellationToken _disposedCancellation;
    private ChatMessageViewModel? _persistedAssistant;
    private int _persistedAssistantLength;

    public AgentTask Task { get; set; } = task;
    public ProjectProfile? Project { get; } = project;
    public AiProvider Provider { get; } = provider;
    public ModelProfile Model { get; } = model;
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = messages;
    public ChatMessageViewModel Assistant { get; internal set; } = assistant;
    public string PendingAssistantContent => ReferenceEquals(_persistedAssistant, Assistant)
        ? Assistant.Content[_persistedAssistantLength..] : Assistant.Content;
    public void MarkAssistantPersisted()
    {
        _persistedAssistant = Assistant;
        _persistedAssistantLength = Assistant.Content.Length;
    }
    public string? AttachmentContext { get; } = attachmentContext;
    public string? AgentInstructions { get; } = agentInstructions;
    public CancellationToken CancellationToken
    {
        get
        {
            lock (_cancellationLock)
            {
                return _cancellation?.Token ?? _disposedCancellation;
            }
        }
    }

    public bool IsCancellationRequested
    {
        get
        {
            lock (_cancellationLock)
            {
                return _cancellation?.IsCancellationRequested ?? _disposedCancellation.IsCancellationRequested;
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
            if (_cancellation is null) return;
            _disposedCancellation = _cancellation.Token;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }
}
