using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.Terminal;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalVtProjectionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static string Text(TerminalFrame? frame) => string.Concat(frame!.Rows.SelectMany(row => row).Select(cell => cell.Text));

    private static async Task WaitAsync(Func<bool> predicate)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        while (!predicate())
        {
            await Task.Delay(10, cancellation.Token);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public Task RawTextTruncationNeverTruncatesTheParserStream() => _session.DispatchAsync(async () =>
    {
        var source = new TerminalStateProbe();
        var context = Context();
        var session = Guid.NewGuid();
        source.Emit(new(context.TaskId, session, 1, TerminalSessionState.Running));
        using var model = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        model.AttachTask(context.TaskId, context.WorkingDirectory, context);
        await Task.Run(() =>
        {
            source.EmitOutput(context.TaskId, new string('x', 250000) + "\u001b[31");
            source.EmitOutput(context.TaskId, "mZ");
        }, TestContext.Current.CancellationToken);
        await WaitAsync(() => model.Frame?.Rows.SelectMany(row => row).Any(cell => cell.Text == "Z") == true);
        Assert.Equal(TerminalOutputBuffer.MaximumCharacters, model.Output.Length);
        Assert.True(model.HasTruncatedOutput);
        var cell = Assert.Single(model.Frame!.Rows.SelectMany(row => row), item => item.Text == "Z");
        Assert.Equal(0xFFCD0000u, cell.Foreground);
        Assert.False(cell.DefaultForeground);
        Assert.InRange(model.Frame.Rows.Length, 30, 158);
        Assert.Equal(0, source.DirectCalls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TaskSwitchRestoresScreenButCannotAcceptOldSessionFrames() => _session.DispatchAsync(async () =>
    {
        var source = new TerminalStateProbe();
        using var model = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        var first = Context();
        var second = Context();
        var session = Guid.NewGuid();
        source.Emit(new(first.TaskId, session, 1, TerminalSessionState.Running));
        model.AttachTask(first.TaskId, first.WorkingDirectory, first);
        source.EmitOutput(first.TaskId, "first");
        await WaitAsync(() => Text(model.Frame) == "first");
        model.AttachTask(second.TaskId, second.WorkingDirectory, second);
        source.EmitOutput(first.TaskId, " background");
        model.AttachTask(first.TaskId, first.WorkingDirectory, first);
        Assert.Equal("first background", Text(model.Frame));
        var replacement = Guid.NewGuid();
        source.Emit(new(first.TaskId, replacement, 2, TerminalSessionState.Running));
        source.EmitOutput(first.TaskId, "replacement");
        await WaitAsync(() => model.Frame?.SessionId == replacement && Text(model.Frame) == "replacement");
        source.Emit(new(first.TaskId, session, 1, TerminalSessionState.Running));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("replacement", Text(model.Frame));
        Assert.Equal(replacement, model.Frame!.SessionId);
    }, TestContext.Current.CancellationToken);
}
