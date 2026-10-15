using System.Collections.Concurrent;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Mcp;

public sealed class StdioMcpClient(McpServerProfile profile, ITaskProcessLauncher processes) : IMcpClient
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = [];
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private ITaskProcess? _process;
    private TextWriter? _writer;
    private Task? _readLoop;
    private Task? _errorLoop;
    private Task? _exitLoop;
    private long _nextId;
    private int _disposed;
    private bool _initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        await WaitForGateAsync(_initializationGate, cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            Start(linked.Token);
            await SendRequestAsync("initialize", new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new { name = "MossAgent", version = "0.1.0" }
            }, cancellationToken).ConfigureAwait(false);
            await SendNotificationAsync("notifications/initialized", new { }, cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        catch
        {
            await CloseConnectionAsync().ConfigureAwait(false);
            throw;
        }
        finally { _initializationGate.Release(); }
    }

    public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync("tools/list", new { }, cancellationToken).ConfigureAwait(false);
        return McpProtocolParser.ReadTools(result);
    }

    public async Task<McpCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync(
            "tools/call", new { name, arguments }, cancellationToken).ConfigureAwait(false);
        return McpProtocolParser.ReadCallResult(result);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await CloseConnectionAsync().ConfigureAwait(false);
        if (_readLoop is not null) await _readLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_errorLoop is not null) await _errorLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_exitLoop is not null) await _exitLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await _initializationGate.WaitAsync().ConfigureAwait(false);
        await _writeGate.WaitAsync().ConfigureAwait(false);
        if (_process is not null) await _process.DisposeAsync().ConfigureAwait(false);
        _initializationGate.Dispose();
        _writeGate.Dispose();
        _shutdown.Dispose();
    }

    private void Start(CancellationToken cancellationToken)
    {
        if (_process is not null)
        {
            return;
        }

        var command = profile.Command
            ?? throw new InvalidOperationException("stdio MCP server 缺少启动命令。");
        var arguments = JsonSerializer.Deserialize<string[]>(profile.ArgumentsJson) ?? [];
        var environment = JsonSerializer.Deserialize<Dictionary<string, string?>>(profile.EnvironmentJson) ?? [];
        _process = processes.Start(new TaskProcessStartRequest(command,
            profile.WorkingDirectory ?? Environment.CurrentDirectory, arguments,
            RedirectStandardInput: true, Environment: environment), cancellationToken);
        _writer = _process.StandardInput ?? throw new IOException("MCP server 没有可写输入流。");
        _readLoop = ReadLoopAsync(_process.StandardOutput, _shutdown.Token);
        _errorLoop = DrainErrorAsync(_process.StandardError, _shutdown.Token);
        _exitLoop = ObserveExitAsync(_process);
    }

    private async Task<JsonElement> SendRequestAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        var sent = false;
        try
        {
            await WriteAsync(new { jsonrpc = "2.0", id, method, @params = parameters }, cancellationToken).ConfigureAwait(false);
            sent = true;
            return await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (sent && cancellationToken.IsCancellationRequested
            && method != "initialize")
        {
            await NotifyCancellationAsync(id).ConfigureAwait(false);
            throw;
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task NotifyCancellationAsync(long id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await SendNotificationAsync("notifications/cancelled", new { requestId = id }, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception) { }
    }

    private Task SendNotificationAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken) =>
        WriteAsync(new { jsonrpc = "2.0", method, @params = parameters }, cancellationToken);

    private async Task WriteAsync(object message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var writer = _writer ?? throw new InvalidOperationException("MCP server 尚未启动。");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        await WaitForGateAsync(_writeGate, cancellationToken).ConfigureAwait(false);
        Task? write = null;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var frame = JsonSerializer.Serialize(message);
            if (frame.Length > 2 * 1024 * 1024) throw new IOException("MCP 请求超过协议消息大小限制。");
            write = writer.WriteLineAsync(frame.AsMemory(), _shutdown.Token);
            await write.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (write is not null)
            {
                await CloseConnectionAsync().ConfigureAwait(false);
                await write.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                throw new IOException("MCP server 连接已关闭。");
            throw;
        }
        finally { _writeGate.Release(); }
    }

    private async Task ReadLoopAsync(TextReader reader, CancellationToken cancellationToken)
    {
        try
        {
            var messages = new McpStdioMessageReader(reader);
            while (await messages.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("method", out _) || !root.TryGetProperty("id", out var idValue)
                    || !idValue.TryGetInt64(out var id)
                    || !_pending.TryRemove(id, out var completion))
                {
                    continue;
                }

                if (root.TryGetProperty("error", out _))
                {
                    completion.TrySetException(new IOException("MCP server 返回协议错误。"));
                }
                else if (root.TryGetProperty("result", out var result))
                {
                    completion.TrySetResult(result.Clone());
                }
                else completion.TrySetException(new IOException("MCP server 响应缺少结果。"));
            }
        }
        catch (Exception) { }
        finally { await CloseConnectionAsync().ConfigureAwait(false); }
    }

    private async Task ObserveExitAsync(ITaskProcess process)
    {
        try { await process.WaitForExitAsync(_shutdown.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
        await CloseConnectionAsync().ConfigureAwait(false);
    }

    private static async Task DrainErrorAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        try { while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false) != 0) { } }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task CloseConnectionAsync()
    {
        _shutdown.Cancel();
        foreach (var completion in _pending.Values)
            completion.TrySetException(new IOException("MCP server 连接已关闭或响应无效。"));
        _pending.Clear();
        if (_process is not null) await _process.TerminateAsync().ConfigureAwait(false);
    }

    private async Task WaitForGateAsync(SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        try { await gate.WaitAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("MCP server 连接已关闭。");
        }
    }
}
