using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

internal sealed class TerminalPaneOperation : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    public TerminalPaneOperation(ToolExecutionContext context, int generation, CancellationToken token)
    {
        Context = context;
        Generation = generation;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Token = _cancellation.Token;
    }

    public ToolExecutionContext Context { get; }
    public int Generation { get; }
    public CancellationToken Token { get; }
    public void Cancel()
    {
        try { _cancellation.Cancel(); }
        catch (AggregateException) { }
    }
    public void Dispose() => _cancellation.Dispose();
}
