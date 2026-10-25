using System.Text.Json;
using MossAgent.Mcp;

namespace MossAgent.Tools.Tests;

internal sealed class GatedMcpClient : IMcpClient
{
    private int _disposed;

    public TaskCompletionSource CallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CallRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<McpToolDefinition>>([]);

    public async Task<McpCallResult> CallToolAsync(
        string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        Calls++;
        CallStarted.TrySetResult();
        await CallRelease.Task.WaitAsync(cancellationToken);
        return new McpCallResult(false, "done");
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Disposed.TrySetResult();
            CallRelease.TrySetException(new IOException("MCP client disposed mid-call."));
        }

        return ValueTask.CompletedTask;
    }
}
