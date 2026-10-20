using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSteeringDraftLease(ConversationDraftUpdate update, Action release) : IDisposable
{
    private Action? _release = release;
    public ConversationDraftUpdate Update { get; } = update;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
