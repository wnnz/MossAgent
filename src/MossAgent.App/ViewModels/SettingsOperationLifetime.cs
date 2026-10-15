namespace MossAgent.App.ViewModels;

internal sealed class SettingsOperationLifetime : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TimeSpan _timeout;
    private bool _disposed;

    public SettingsOperationLifetime(TimeSpan? operationTimeout = null)
    {
        _timeout = operationTimeout ?? TimeSpan.FromSeconds(30);
        Token = _lifetime.Token;
    }

    public CancellationToken Token { get; }

    public CancellationTokenSource CreateOperation(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Token);
        try
        {
            operation.CancelAfter(_timeout);
            return operation;
        }
        catch
        {
            operation.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _lifetime.Cancel(); }
        finally { _lifetime.Dispose(); }
    }
}
