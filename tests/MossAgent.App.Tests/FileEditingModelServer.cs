using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MossAgent.App.Tests;

internal sealed class FileEditingModelServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly ConcurrentQueue<string> _requests = new();
    private readonly string _readPath;
    private readonly string _mutationPath;
    private readonly bool _replace;
    private readonly bool _delete;
    private readonly FileEditingMutation? _mutation;
    private readonly string _content;
    private readonly Task _serving;

    public FileEditingModelServer(string path, string mutationPath, bool replace, string content, CancellationToken token,
        bool delete = false, FileEditingMutation? mutation = null)
    {
        _readPath = path;
        _mutationPath = mutationPath;
        _replace = replace;
        _delete = delete;
        _mutation = mutation;
        _content = content;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        _serving = ServeAsync(_stopping.Token);
    }

    public Uri BaseUri { get; }
    public IReadOnlyList<string> Requests => _requests.ToArray();
    public string? ExpectedSha256 { get; private set; }
    public Task Completion => _serving;

    private async Task ServeAsync(CancellationToken token)
    {
        for (var turn = 0; turn < 3; turn++)
        {
            using var client = await _listener.AcceptTcpClientAsync(token);
            await using var stream = client.GetStream();
            var request = await ReadBodyAsync(stream, token);
            _requests.Enqueue(request);
            using var document = JsonDocument.Parse(request);
            var response = CreateResponse(document.RootElement, turn);
            var bytes = Encoding.UTF8.GetBytes($"data: {response}\n\ndata: [DONE]\n\n");
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
                + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, token);
            await stream.WriteAsync(bytes, token);
        }
    }

    private string CreateResponse(JsonElement request, int turn)
    {
        if (turn == 2) return JsonSerializer.Serialize(new
        {
            type = "response.output_text.delta", delta = "本机文件流程已结束。"
        });
        var prefix = turn == 0 ? "filesystem_read_file__"
            : _mutation?.ToolPrefix ?? (_delete ? "filesystem_delete_path__" : _replace ? "filesystem_replace_text__" : "filesystem_write_file__");
        var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(tool => tool?.StartsWith(prefix, StringComparison.Ordinal) == true);
        object arguments;
        if (turn == 0) arguments = new { path = _readPath };
        else
        {
            var readOutput = request.GetProperty("input").EnumerateArray().Single(item =>
                item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output")
                .GetProperty("output").GetString()!;
            using var read = JsonDocument.Parse(readOutput);
            ExpectedSha256 = Regex.Match(read.RootElement.GetProperty("Summary").GetString()!,
                "[A-Fa-f0-9]{64}").Value;
            if (ExpectedSha256.Length != 64) throw new InvalidDataException("Read response omitted its full-file hash.");
            if (_mutation is not null) arguments = _mutation.Arguments;
            else arguments = _delete ? new { path = _mutationPath } : _replace
                ? new { path = _mutationPath, oldText = "alpha", newText = "beta", expectedSha256 = ExpectedSha256 }
                : new { path = _mutationPath, content = _content, expectedSha256 = ExpectedSha256 };
        }
        return JsonSerializer.Serialize(new
        {
            type = "response.output_item.done",
            item = new { type = "function_call", call_id = turn == 0 ? "file-read" : "file-edit",
                name, arguments = JsonSerializer.Serialize(arguments) }
        });
    }

    private static async Task<string> ReadBodyAsync(NetworkStream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var chunk = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        while (received.Length <= 1_000_000)
        {
            var count = await stream.ReadAsync(chunk, token);
            if (count == 0) throw new EndOfStreamException();
            received.Write(chunk, 0, count);
            var bytes = received.ToArray();
            if (bodyStart < 0 && bytes.AsSpan().IndexOf("\r\n\r\n"u8) is var headerEnd and >= 0)
            {
                bodyStart = headerEnd + 4;
                var header = Encoding.ASCII.GetString(bytes, 0, bodyStart);
                var length = header.Split("\r\n").Single(line =>
                    line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                bodyLength = int.Parse(length.Split(':', 2)[1].Trim());
                if (bodyLength is < 0 or > 1_000_000) throw new InvalidDataException("Unbounded model request.");
            }
            if (bodyStart >= 0 && bytes.Length >= bodyStart + bodyLength)
                return Encoding.UTF8.GetString(bytes, bodyStart, bodyLength);
        }
        throw new InvalidDataException("Model request exceeded fixture limit.");
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        try { await _serving; }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        _stopping.Dispose();
    }
}
