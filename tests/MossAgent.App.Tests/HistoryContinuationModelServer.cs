using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MossAgent.App.Tests;

internal sealed class HistoryContinuationModelServer : IAsyncDisposable
{
    public const string Evidence = "HISTORY_EVIDENCE_74623";
    public const string FirstReply = "第一次读取已完成，等待下一条指令。";
    public const string ContinuedReply = "此前文件的历史结果仍在上下文中，没有重新执行读取。";
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly ConcurrentQueue<JsonElement> _requests = new();
    private readonly string _path;
    private readonly bool _mutation;
    private readonly int _startingTurn;
    private readonly Task _serving;
    private string _readHash = string.Empty;
    private const string Completed =
        "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"fixture-response\",\"status\":\"completed\"}}\n\n";

    public HistoryContinuationModelServer(string path, bool mutation, CancellationToken token, int startingTurn = 0)
    {
        _path = path;
        _mutation = mutation;
        _startingTurn = startingTurn;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        _serving = ServeAsync(_stopping.Token);
    }

    public Uri BaseUri { get; }
    public IReadOnlyList<JsonElement> Requests => _requests.ToArray();
    public bool SawHistoricalEvidence { get; private set; }
    public bool FollowupHadNoNativeReplay { get; private set; }
    public Task Completion => _serving;

    private async Task ServeAsync(CancellationToken token)
    {
        for (var turn = _startingTurn; turn < (_mutation ? 4 : 3); turn++)
        {
            using var client = await _listener.AcceptTcpClientAsync(token);
            await using var stream = client.GetStream();
            using var document = JsonDocument.Parse(await ReadBodyAsync(stream, token));
            var request = document.RootElement;
            _requests.Enqueue(request.Clone());
            var response = CreateResponse(request, turn);
            var bytes = Encoding.UTF8.GetBytes($"data: {response}\n\n" + Completed + "data: [DONE]\n\n");
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
                + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, token);
            await stream.WriteAsync(bytes, token);
        }
    }

    private string CreateResponse(JsonElement request, int turn)
    {
        if (turn == 1)
        {
            var readOutput = request.GetProperty("input").EnumerateArray().Single(item =>
                item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output").GetProperty("output").GetString()!;
            using var read = JsonDocument.Parse(readOutput);
            _readHash = Regex.Match(read.RootElement.GetProperty("Summary").GetString()!, "[A-Fa-f0-9]{64}").Value;
        }
        if (turn == 2)
        {
            var input = request.GetProperty("input").EnumerateArray().ToArray();
            SawHistoricalEvidence = input.Any(item => item.TryGetProperty("role", out var role) && role.GetString() == "user"
                && item.GetProperty("content").GetString() is { } text
                && text.StartsWith("Historical tool output.", StringComparison.Ordinal) && text.Contains(Evidence, StringComparison.Ordinal));
            FollowupHadNoNativeReplay = input.All(item => !item.TryGetProperty("type", out _));
        }
        if (turn == 0 || (turn == 2 && _mutation))
        {
            var prefix = turn == 0 ? "filesystem_read_file__" : "filesystem_write_file__";
            var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
                .Single(name => name?.StartsWith(prefix, StringComparison.Ordinal) == true);
            object arguments = turn == 0 ? new { path = _path }
                : new { path = _path, content = "forbidden mutation", expectedSha256 = _readHash };
            return JsonSerializer.Serialize(new
            {
                type = "response.output_item.done",
                item = new
                {
                    type = "function_call",
                    call_id = turn == 0 ? "history-read" : "history-write",
                    name,
                    arguments = JsonSerializer.Serialize(arguments)
                }
            });
        }
        return JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = turn == 1 ? FirstReply : ContinuedReply });
    }

    private static async Task<string> ReadBodyAsync(NetworkStream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var buffer = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        while (received.Length <= 1_000_000)
        {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0) throw new EndOfStreamException();
            received.Write(buffer, 0, count);
            var bytes = received.ToArray();
            if (bodyStart < 0 && bytes.AsSpan().IndexOf("\r\n\r\n"u8) is var headerEnd and >= 0)
            {
                bodyStart = headerEnd + 4;
                var header = Encoding.ASCII.GetString(bytes, 0, bodyStart);
                var length = header.Split("\r\n").Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                bodyLength = int.Parse(length.Split(':', 2)[1].Trim());
                if (bodyLength is < 0 or > 1_000_000) throw new InvalidDataException("Unbounded fixture request.");
            }
            if (bodyStart >= 0 && bytes.Length >= bodyStart + bodyLength)
                return Encoding.UTF8.GetString(bytes, bodyStart, bodyLength);
        }
        throw new InvalidDataException("Fixture request exceeded its bound.");
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try { await _serving.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        _stopping.Dispose();
    }
}
