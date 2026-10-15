using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Terminal;

internal sealed class TerminalResizeOperation(ToolExecutionContext context, Guid sessionId, int generation, CancellationToken token) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
    public ToolExecutionContext Context { get; } = context;
    public Guid SessionId { get; } = sessionId;
    public int Generation { get; } = generation;
    public CancellationToken Token => _cancellation.Token;
    public void SetDeadline() => _cancellation.CancelAfter(TimeSpan.FromSeconds(30));
    public void Cancel() { try { _cancellation.Cancel(); } catch (AggregateException) { } }
    public void Dispose() => _cancellation.Dispose();
}
