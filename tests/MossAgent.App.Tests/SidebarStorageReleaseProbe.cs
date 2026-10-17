using Xunit;

namespace MossAgent.App.Tests;

internal sealed class SidebarStorageReleaseProbe : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent-sidebar-stop", Guid.NewGuid().ToString("N"));
    private Task? _operation;

    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string FilePath => Path.Combine(_root, "synthetic-preference.json");
    public CancellationToken Token { get; private set; }

    public Task BlockAsync(CancellationToken token) => _operation = HoldFileAsync(token);

    private async Task HoldFileAsync(CancellationToken token)
    {
        Token = token;
        using var registration = token.Register(() => Cancelled.TrySetResult());
        Directory.CreateDirectory(_root);
        using var file = File.Open(FilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        Entered.TrySetResult();
        await Release.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Release.TrySetResult();
        if (_operation is not null) await _operation;
        if (File.Exists(FilePath)) File.Delete(FilePath);
        if (Directory.Exists(_root)) Directory.Delete(_root);
    }
}
