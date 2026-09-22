using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Mcp;

public sealed class StdioMcpClient(McpServerProfile profile) : IMcpClient
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = [];
    private readonly CancellationTokenSource _shutdown = new();
    private Process? _process;
    private StreamWriter? _writer;
    private Task? _readLoop;
    private long _nextId;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Start();
        await SendRequestAsync("initialize", new
        {
            protocolVersion = "2025-03-26",
            capabilities = new { },
            clientInfo = new { name = "MossAgent", version = "0.1.0" }
        }, cancellationToken);
        await SendNotificationAsync("notifications/initialized", new { }, cancellationToken);
    }

    public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync("tools/list", new { }, cancellationToken);
        return McpProtocolParser.ReadTools(result);
    }

    public async Task<McpCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync(
            "tools/call", new { name, arguments }, cancellationToken);
        return McpProtocolParser.ReadCallResult(result);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        if (_readLoop is not null)
        {
            await _readLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _writer?.Dispose();
        _process?.Dispose();
        _shutdown.Dispose();
    }

    private void Start()
    {
        if (_process is not null)
        {
            return;
        }

        var command = profile.Command
            ?? throw new InvalidOperationException("stdio MCP server 缺少启动命令。");
        var startInfo = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = profile.WorkingDirectory ?? Environment.CurrentDirectory
        };
        foreach (var argument in JsonSerializer.Deserialize<string[]>(profile.ArgumentsJson) ?? [])
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(profile.EnvironmentJson) ?? [])
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        _process = new Process { StartInfo = startInfo };
        _process.ErrorDataReceived += static (_, _) => { };
        _process.Start();
        _process.BeginErrorReadLine();
        _writer = _process.StandardInput;
        _writer.AutoFlush = true;
        _readLoop = ReadLoopAsync(_process.StandardOutput, _shutdown.Token);
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
        await WriteAsync(new { jsonrpc = "2.0", id, method, @params = parameters }, cancellationToken);
        return await completion.Task;
    }

    private Task SendNotificationAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken) =>
        WriteAsync(new { jsonrpc = "2.0", method, @params = parameters }, cancellationToken);

    private async Task WriteAsync(object message, CancellationToken cancellationToken)
    {
        var writer = _writer ?? throw new InvalidOperationException("MCP server 尚未启动。");
        await writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
    }

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var idValue)
                    || !idValue.TryGetInt64(out var id)
                    || !_pending.TryRemove(id, out var completion))
                {
                    continue;
                }

                if (root.TryGetProperty("error", out var error))
                {
                    completion.TrySetException(new InvalidOperationException(error.GetRawText()));
                }
                else if (root.TryGetProperty("result", out var result))
                {
                    completion.TrySetResult(result.Clone());
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            foreach (var completion in _pending.Values)
            {
                completion.TrySetException(new IOException("MCP server 连接已关闭。"));
            }

            _pending.Clear();
        }
    }
}
