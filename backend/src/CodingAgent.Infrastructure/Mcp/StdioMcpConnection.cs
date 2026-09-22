using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Mcp;

/// <summary>stdio 传输 MCP 连接：子进程 + stdin/stdout 行分隔 JSON-RPC 2.0。</summary>
public class StdioMcpConnection(McpServer server) : IMcpConnection
{
    public int ServerId => server.Id;
    public string ServerName => server.Name;
    public bool IsConnected { get; private set; }

    private Process? _process;
    private StreamWriter? _stdin;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly ConcurrentDictionary<int, string> _pendingResponses = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private int _nextId;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsConnected || string.IsNullOrWhiteSpace(server.Command))
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = server.Command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrWhiteSpace(server.ArgsJson))
        {
            var args = JsonSerializer.Deserialize<List<string>>(server.ArgsJson);
            foreach (var arg in args ?? [])
            {
                psi.ArgumentList.Add(arg);
            }
        }
        if (!string.IsNullOrWhiteSpace(server.EnvJson))
        {
            var env = JsonSerializer.Deserialize<Dictionary<string, string>>(server.EnvJson);
            foreach (var (k, v) in env ?? [])
            {
                psi.Environment[k] = v;
            }
        }

        _process = Process.Start(psi) ?? throw new InvalidOperationException($"无法启动 MCP 进程: {server.Command}");
        _stdin = _process.StandardInput;

        try
        {
            // 读循环使用连接自身生命周期的 CTS，与调用方请求级 ct 分离（否则请求返回后读循环退出）
            _ = Task.Run(() => ReadOutputLoopAsync(_process, _lifetimeCts.Token), CancellationToken.None);

            // 仅 initialize 握手使用调用方 ct
            await RequestAsync("initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "CodingAgent", version = "1.0.0" },
            }, ct);
            await NotifyAsync("notifications/initialized", ct);
            IsConnected = true;
        }
        catch
        {
            // 初始化失败：清理子进程，避免泄漏
            await KillProcessAsync();
            throw;
        }
    }

    private async Task ReadOutputLoopAsync(Process process, CancellationToken lifetime)
    {
        try
        {
            while (!process.HasExited && !lifetime.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(lifetime);
                if (line is null)
                {
                    break;
                }
                HandleLine(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* 进程输出读取失败时静默：后续调用会超时报错 */ }
    }

    private void HandleLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                _pendingResponses[idProp.GetInt32()] = line;
            }
        }
        catch (JsonException) { /* 忽略非 JSON 行 */ }
    }

    private async Task<JsonElement> RequestAsync(string method, object? parameters, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextId);
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });

        await _ioLock.WaitAsync(ct);
        try
        {
            _pendingResponses.TryRemove(id, out _);
            await _stdin!.WriteLineAsync(payload);
            await _stdin.FlushAsync(ct);

            var deadline = TimeSpan.FromSeconds(30);
            var start = DateTime.UtcNow;
            while (!_pendingResponses.TryGetValue(id, out var response))
            {
                if (DateTime.UtcNow - start > deadline)
                {
                    throw new TimeoutException($"MCP 请求超时: {method}");
                }
                await Task.Delay(20, ct);
            }
            var result = _pendingResponses[id];
            _pendingResponses.TryRemove(id, out _);

            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : "MCP 错误";
                throw new InvalidOperationException($"MCP 错误: {message}");
            }
            return root.TryGetProperty("result", out var resultEl) ? resultEl.Clone() : JsonSerializer.Deserialize<JsonElement>("{}");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task NotifyAsync(string method, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", method });
        await _ioLock.WaitAsync(ct);
        try
        {
            await _stdin!.WriteLineAsync(payload);
            await _stdin.FlushAsync(ct);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<List<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await RequestAsync("tools/list", new { }, ct);
        var tools = new List<McpToolInfo>();
        if (result.TryGetProperty("tools", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in arr.EnumerateArray())
            {
                tools.Add(new McpToolInfo
                {
                    Name = t.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    Description = t.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                    SchemaJson = t.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : "{}",
                });
            }
        }
        return tools;
    }

    public async Task<string> CallToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        var args = string.IsNullOrWhiteSpace(argumentsJson) ? (object)new { } : JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var result = await RequestAsync("tools/call", new { name = toolName, arguments = args }, ct);

        if (result.TryGetProperty("isError", out var isError) && isError.GetBoolean())
        {
            return $"工具执行错误: {ExtractText(result)}";
        }
        return ExtractText(result);
    }

    private static string ExtractText(JsonElement result)
    {
        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var c in content.EnumerateArray())
            {
                if (c.TryGetProperty("type", out var type) && type.GetString() == "text" && c.TryGetProperty("text", out var text))
                {
                    sb.AppendLine(text.GetString());
                }
            }
            return sb.ToString().TrimEnd();
        }
        return result.GetRawText();
    }

    private async Task KillProcessAsync()
    {
        try
        {
            if (_process is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception) { }
    }

    public async ValueTask DisposeAsync()
    {
        IsConnected = false;
        _lifetimeCts.Cancel();
        try
        {
            if (_stdin is not null)
            {
                await _stdin.FlushAsync();
                _stdin.Dispose();
            }
        }
        catch (Exception) { }
        await KillProcessAsync();
        _process?.Dispose();
        _lifetimeCts.Dispose();
        _ioLock.Dispose();
    }
}
