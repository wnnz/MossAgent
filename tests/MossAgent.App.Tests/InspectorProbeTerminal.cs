using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.Tests;

internal sealed class InspectorProbeTerminal : ITerminalSessionManager
{
    public event EventHandler<TerminalOutputEventArgs>? OutputReceived;
    public int DirectCalls { get; private set; }
    public void Emit(Guid taskId, string text) => OutputReceived?.Invoke(this, new TerminalOutputEventArgs(taskId, text));
    public Task StartAsync(Guid taskId, string workingDirectory, CancellationToken cancellationToken) { DirectCalls++; return Task.CompletedTask; }
    public Task WriteAsync(Guid taskId, string input, CancellationToken cancellationToken) { DirectCalls++; return Task.CompletedTask; }
    public Task StopAsync(Guid taskId) { DirectCalls++; return Task.CompletedTask; }
    public void Resize(Guid taskId, int columns, int rows) => DirectCalls++;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
