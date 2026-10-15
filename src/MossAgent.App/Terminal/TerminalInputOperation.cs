using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Terminal;

internal sealed class TerminalInputOperation(ToolExecutionContext context, Guid viewId, Guid sessionId, int generation,
    CancellationToken token) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
    private readonly StringBuilder _pending = new();
    public ToolExecutionContext Context { get; } = context;
    public Guid ViewId { get; } = viewId;
    public Guid SessionId { get; } = sessionId;
    public int Generation { get; } = generation;
    public CancellationToken Token => _cancellation.Token;
    public int InFlightCharacters { get; private set; }
    public bool ExclusivePaste { get; init; }
    public bool HasPending => _pending.Length > 0;

    public bool Append(string value)
    {
        if (Token.IsCancellationRequested || value.Length + _pending.Length + InFlightCharacters > TerminalKeyEncoder.MaximumCharacters) return false;
        _pending.Append(value);
        return true;
    }

    public string Take()
    {
        var value = _pending.ToString();
        _pending.Clear();
        InFlightCharacters = value.Length;
        return value;
    }

    public void CompleteWrite() => InFlightCharacters = 0;
    public void SetDeadline() => _cancellation.CancelAfter(TimeSpan.FromSeconds(30));
    public void Cancel()
    {
        _pending.Clear();
        try { _cancellation.Cancel(); } catch (AggregateException) { }
    }
    public void Dispose() => _cancellation.Dispose();
}
