using System.Collections.ObjectModel;
using MossAgent.Domain;
using MossAgent.Application.Conversations;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunState(
    AgentTask task,
    ProjectProfile? project,
    AiProvider provider,
    ModelProfile model,
    ObservableCollection<ChatMessageViewModel> messages,
    ChatMessageViewModel assistant,
    string? attachmentContext = null,
    string? agentInstructions = null,
    IReadOnlyList<string>? pinnedRoots = null) : IDisposable
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
    /// <summary>Automation-pinned authorized roots; null means the run resolves the
    /// project's current authorized directories when its context is created.</summary>
    public IReadOnlyList<string>? PinnedRoots { get; } = pinnedRoots;
    public ConversationSteeringSession? Steering { get; internal set; }
    public bool AuthorizationChanged { get; internal set; }
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

    public string Activity { get; set; } = UiText.T("准备运行…");

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

