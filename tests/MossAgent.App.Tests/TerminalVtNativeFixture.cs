using System.Text.Json;
using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class TerminalVtNativeFixture : IAsyncDisposable
{
    private readonly ToolExecutor _executor;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", "vt-" + Guid.NewGuid().ToString("N"));
    public PtyTerminalSessionManager Manager { get; } = new();
    public TerminalScreenStore Store { get; } = new(true);
    public ToolExecutionContext Context { get; }
    public IToolExecutor Executor => _executor;

    public TerminalVtNativeFixture()
    {
        Directory.CreateDirectory(_root);
        Context = new(Guid.NewGuid(), _root, [_root], ApprovalPolicy.FullAccess, Timeout: TimeSpan.FromSeconds(20));
        Store.Attach(Context, null);
        Manager.StateChanged += (_, args) => Store.ApplyState(args.Snapshot);
        Manager.OutputReceived += (_, args) => Store.Append(args, Manager.GetSnapshot(args.TaskId));
        _executor = new(new ToolRegistry([
            new TerminalStartTool(Manager, new AuthorizedPathResolver()),
            new TerminalWriteTool(Manager, new AuthorizedPathResolver()), new TerminalStopTool(Manager),
            new TerminalResizeTool(Manager, new AuthorizedPathResolver())]),
            new PolicyApprovalService((_, _, _) => ValueTask.FromResult(false)));
    }

    public Task<ToolResult> ExecuteAsync(string name, object arguments, ToolExecutionContext? context = null) =>
        _executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), name, JsonSerializer.SerializeToElement(arguments)),
            context ?? Context, TestContext.Current.CancellationToken);

    public async Task<TerminalFrame> WaitFrameAsync(Func<TerminalFrame, bool> predicate)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var frame = Store.Capture();
            if (frame is not null && predicate(frame)) return frame;
            await Task.Delay(20, cancellation.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await Manager.DisposeAsync(); }
        finally
        {
            Store.Dispose();
            var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests"));
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(_root)), expectedParent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup path escaped its owned parent.");
            for (var attempt = 0; ; attempt++)
            {
                try { Directory.Delete(_root, recursive: true); break; }
                catch (IOException) when (attempt < 50)
                { await Task.Delay(50, TestContext.Current.CancellationToken); }
            }
        }
    }
}
