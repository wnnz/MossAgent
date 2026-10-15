using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.Tests;

internal sealed class TerminalStateProbe : ITerminalSessionManager, ITerminalStateSource
{
    private readonly Dictionary<Guid, TerminalSessionSnapshot> _snapshots = [];
    private EventHandler<TerminalStateEventArgs>? _changed;
    public int StateSubscriptions { get; private set; }
    public int DirectCalls { get; private set; }
    public Func<Guid, TerminalSessionSnapshot?>? Read { get; set; }
    public event EventHandler<TerminalOutputEventArgs>? OutputReceived;
    public event EventHandler<TerminalStateEventArgs>? StateChanged
    {
        add { _changed += value; StateSubscriptions++; }
        remove { _changed -= value; StateSubscriptions--; }
    }
    public TerminalSessionSnapshot? GetSnapshot(Guid taskId) => Read is null ? _snapshots.GetValueOrDefault(taskId) : Read(taskId);
    public void Emit(TerminalSessionSnapshot snapshot)
    {
        if (!_snapshots.TryGetValue(snapshot.TaskId, out var current) || snapshot.Revision > current.Revision) _snapshots[snapshot.TaskId] = snapshot;
        _changed?.Invoke(this, new TerminalStateEventArgs(snapshot));
    }
    public void EmitOutput(Guid taskId, string text) => OutputReceived?.Invoke(this, new TerminalOutputEventArgs(taskId, text, GetSnapshot(taskId)?.SessionId));
    public Task StartAsync(Guid taskId, string workingDirectory, CancellationToken cancellationToken) { DirectCalls++; throw new InvalidOperationException(); }
    public Task WriteAsync(Guid taskId, string input, CancellationToken cancellationToken) { DirectCalls++; throw new InvalidOperationException(); }
    public Task StopAsync(Guid taskId) { DirectCalls++; throw new InvalidOperationException(); }
    public void Resize(Guid taskId, int columns, int rows) { DirectCalls++; throw new InvalidOperationException(); }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
