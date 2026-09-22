using System.Collections.Concurrent;

namespace CodingAgent.Api.Sse;

/// <summary>会话级取消源映射：每请求一个 CTS，TryCancel 取消当前 owner；请求结束仅当自己是 owner 才清理。</summary>
public class SessionCancellationMap
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _map = new();

    /// <summary>指定会话是否已有运行中的 chat。</summary>
    public bool IsRunning(Guid sessionId) => _map.ContainsKey(sessionId);

    /// <summary>开始一次运行（调用方须先确认 IsRunning 为 false）。</summary>
    public CancellationTokenSource Start(Guid sessionId)
    {
        var cts = new CancellationTokenSource();
        _map.AddOrUpdate(sessionId, cts, (_, old) =>
        {
            // 竞态兜底：旧 owner 直接取消并丢弃
            old.Cancel();
            old.Dispose();
            return cts;
        });
        return cts;
    }

    /// <summary>取消指定会话当前运行；返回是否取消了正在进行的运行。</summary>
    public bool TryCancel(Guid sessionId)
    {
        if (_map.TryGetValue(sessionId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    /// <summary>结束运行：仅当自己是当前 owner 才移除，避免误清理并发请求。</summary>
    public void End(Guid sessionId, CancellationTokenSource cts)
    {
        if (_map.TryGetValue(sessionId, out var current) && ReferenceEquals(current, cts))
        {
            _map.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(sessionId, cts));
        }
        cts.Dispose();
    }
}
