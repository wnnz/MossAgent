using System.Collections.Concurrent;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class PtyTerminalSessionManager : ITerminalSessionManager
{
    private readonly ConcurrentDictionary<Guid, PtyTerminalSession> _sessions = [];

    public event EventHandler<TerminalOutputEventArgs>? OutputReceived;

    public async Task StartAsync(
        Guid taskId,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (_sessions.ContainsKey(taskId))
        {
            return;
        }

        var session = await PtyTerminalSession.StartAsync(
            workingDirectory,
            output => OutputReceived?.Invoke(this, new TerminalOutputEventArgs(taskId, output)),
            cancellationToken);
        if (!_sessions.TryAdd(taskId, session))
        {
            await session.DisposeAsync();
        }
    }

    public Task WriteAsync(Guid taskId, string input, CancellationToken cancellationToken)
    {
        return Get(taskId).WriteAsync(input, cancellationToken);
    }

    public void Resize(Guid taskId, int columns, int rows) => Get(taskId).Resize(columns, rows);

    public async Task StopAsync(Guid taskId)
    {
        if (_sessions.TryRemove(taskId, out var session))
        {
            await session.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var taskId in _sessions.Keys)
        {
            await StopAsync(taskId);
        }
    }

    private PtyTerminalSession Get(Guid taskId) =>
        _sessions.TryGetValue(taskId, out var session)
            ? session
            : throw new InvalidOperationException("当前任务没有活动终端。");
}

