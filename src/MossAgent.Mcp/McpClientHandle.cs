using System.Text.Json;

namespace MossAgent.Mcp;

/// <summary>
/// Owns one connected client generation. Calls admitted before draining
/// complete on the wrapped client; calls arriving after draining starts are
/// rejected so a reload can never retarget a call to a newer server schema.
/// </summary>
internal sealed class McpClientHandle(IMcpClient client) : IMcpClient
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _inFlight;
    private bool _draining;

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        client.InitializeAsync(cancellationToken);

    public Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken) =>
        client.ListToolsAsync(cancellationToken);

    public async Task<McpCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        EnterCall();
        try
        {
            return await client.CallToolAsync(name, arguments, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ExitCall();
        }
    }

    public async Task DrainAsync(TimeSpan timeout)
    {
        Task drained;
        lock (_gate)
        {
            _draining = true;
            drained = _inFlight == 0 ? Task.CompletedTask : _drained.Task;
        }

        try
        {
            await drained.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _draining = true;
        }

        await client.DisposeAsync().ConfigureAwait(false);
    }

    private void EnterCall()
    {
        lock (_gate)
        {
            if (_draining) throw new McpReconnectingException();
            _inFlight++;
        }
    }

    private void ExitCall()
    {
        lock (_gate)
        {
            if (--_inFlight == 0) _drained.TrySetResult();
        }
    }
}
