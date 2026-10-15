using Avalonia.Headless;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalClipboardCommandsTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static TerminalFrame Frame()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("A中e\u0301🚀");
        return screen.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false);
    }

    [Fact]
    public Task ExplicitCopyUsesOnlyRenderedSelectionAndNoInput() => _session.DispatchAsync(async () =>
    {
        var frame = Frame();
        var selection = new TerminalSelection();
        selection.Observe(frame);
        selection.SelectAll();
        string? copied = null;
        var reads = 0;
        var inputs = 0;
        var actions = new TerminalClipboardCommands(selection, () => true, text => { copied = text; return Task.CompletedTask; },
            () => { reads++; return Task.FromResult<string?>("never-read"); });
        actions.Observe(frame, new RelayCommand<TerminalInputRequest>(_ => inputs++), 7);
        Assert.Equal(0, reads);
        await actions.CopyCommand.ExecuteAsync(null);
        Assert.Equal("A中e\u0301🚀", copied);
        Assert.Equal(0, inputs);
        Assert.Equal(0, reads);
        Assert.Contains("已复制", actions.Notice);
        actions.Observe(null, null, 8);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("single", false)]
    [InlineData("multi\r\nline", true)]
    public Task PasteRequiresGestureAndMultilineConfirmation(string text, bool confirm) => _session.DispatchAsync(async () =>
    {
        var frame = Frame();
        var requests = new List<TerminalInputRequest>();
        var actions = new TerminalClipboardCommands(new(), () => true, _ => Task.CompletedTask, () => Task.FromResult<string?>(text));
        actions.Observe(frame, new RelayCommand<TerminalInputRequest>(request => requests.Add(request!)), 7);
        Assert.Empty(requests);
        await actions.PasteCommand.ExecuteAsync(null);
        Assert.Equal(confirm, actions.HasPendingPaste);
        if (confirm)
        {
            Assert.Empty(requests);
            actions.ConfirmPasteCommand.Execute(null);
        }
        var request = Assert.Single(requests);
        Assert.True(request.IsPaste);
        Assert.Equal(7, request.ScopeGeneration);
        Assert.Equal(frame.SessionId, request.SessionId);
        Assert.Equal(text, request.Text);
        actions.Observe(null, null, 8);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("session")]
    [InlineData("scope")]
    [InlineData("mode")]
    [InlineData("hidden")]
    [InlineData("cancel")]
    public Task LateClipboardReadRetainsOwnerButCannotSendToReplacement(string reason) => _session.DispatchAsync(async () =>
    {
        var frame = Frame();
        var available = true;
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        var command = new RelayCommand<TerminalInputRequest>(_ => requests++);
        var actions = new TerminalClipboardCommands(new(), () => available, _ => Task.CompletedTask, () => pending.Task);
        actions.Observe(frame, command, 1);
        var operation = actions.PasteCommand.ExecuteAsync(null);
        Assert.True(actions.IsBusy);
        if (reason == "session") actions.Observe(frame with { SessionId = Guid.NewGuid() }, command, 1);
        if (reason == "scope") actions.Observe(frame, command, 2);
        if (reason == "mode") actions.Observe(frame with { BracketedPaste = true }, command, 1);
        if (reason == "hidden") { available = false; actions.RefreshAvailability(); available = true; }
        if (reason == "cancel") actions.CancelCommand.Execute(null);
        Assert.True(actions.IsBusy);
        Assert.False(actions.PasteCommand.CanExecute(null));
        pending.SetResult("never-sent");
        await operation;
        Assert.False(actions.IsBusy);
        Assert.DoesNotContain("正在等待", actions.Notice);
        Assert.Equal(0, requests);
        Assert.False(actions.HasPendingPaste);
        actions.Observe(null, null, 3);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("\u001b[201~secret")]
    [InlineData("\0secret")]
    public Task InvalidPasteShowsSafeNoticeWithoutInput(string text) => _session.DispatchAsync(async () =>
    {
        var requests = 0;
        var actions = new TerminalClipboardCommands(new(), () => true, _ => Task.CompletedTask, () => Task.FromResult<string?>(text));
        actions.Observe(Frame(), new RelayCommand<TerminalInputRequest>(_ => requests++), 1);
        await actions.PasteCommand.ExecuteAsync(null);
        Assert.Equal(0, requests);
        Assert.Contains("控制字符", actions.Notice);
        Assert.DoesNotContain("secret", actions.Notice);
        actions.Observe(null, null, 2);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisabledInputDoesNotReadAndPendingPasteIsDiscardedWhenScopeChanges() => _session.DispatchAsync(async () =>
    {
        var reads = 0;
        var enabled = false;
        var inputs = 0;
        var command = new RelayCommand<TerminalInputRequest>(_ => inputs++, _ => enabled);
        var frame = Frame();
        var actions = new TerminalClipboardCommands(new(), () => true, _ => Task.CompletedTask,
            () => { reads++; return Task.FromResult<string?>("multi\nline"); });
        actions.Observe(frame, command, 1);
        await actions.PasteCommand.ExecuteAsync(null);
        Assert.Equal(0, reads);
        enabled = true;
        command.NotifyCanExecuteChanged();
        await actions.PasteCommand.ExecuteAsync(null);
        Assert.True(actions.HasPendingPaste);
        actions.Observe(frame, command, 2);
        actions.ConfirmPasteCommand.Execute(null);
        Assert.Equal(0, inputs);
        Assert.False(actions.HasPendingPaste);
        actions.Observe(null, null, 3);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClipboardOwnerRevalidatesSelectionWhenReusedForAnotherSession() => _session.DispatchAsync(async () =>
    {
        var first = Frame();
        var selection = new TerminalSelection();
        var copies = 0;
        var actions = new TerminalClipboardCommands(selection, () => true, _ => { copies++; return Task.CompletedTask; }, () => Task.FromResult<string?>(null));
        var command = new RelayCommand<TerminalInputRequest>(_ => { });
        actions.Observe(first, command, 1);
        selection.SelectAll();
        Assert.True(selection.HasSelection);
        actions.Observe(first with { SessionId = Guid.NewGuid() }, command, 2);
        Assert.False(selection.HasSelection);
        await actions.CopyCommand.ExecuteAsync(null);
        Assert.Equal(0, copies);
        actions.Observe(null, null, 3);
    }, TestContext.Current.CancellationToken);
}
