using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using MossAgent.App.Controls;
using MossAgent.App.Terminal;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalInputMethodNativeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(ApprovalPolicy.FullAccess)]
    [InlineData(ApprovalPolicy.AskEveryTime)]
    [InlineData(ApprovalPolicy.ReadOnly)]
    public Task CommittedUnicodeUsesTheRealToolPipelineAndCurrentPolicy(ApprovalPolicy policy) => _session.DispatchAsync(async () =>
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        const string text = "你好🚀";
        var command = "$b=[Text.StringBuilder]::new(); [Console]::WriteLine('IME-'+'READY'); 1.." + text.Length
            + " | ForEach-Object { $null=$b.Append([Console]::ReadKey($true).KeyChar) }; [Console]::WriteLine(('IME-'+'RESULT:')+[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($b.ToString())))\r";
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = command })).IsSuccess);
        var frame = await fixture.WaitFrameAsync(frame => HasRow(frame, "IME-READY") && frame.Win32InputMode);
        using var input = new TerminalInputController(fixture.Executor, fixture.Manager);
        input.Attach(fixture.Context with { ApprovalPolicy = policy }, 31, TestContext.Current.CancellationToken);
        input.Observe(frame, true);
        var viewport = new AppTerminalViewport { Frame = frame, InputCommand = input.InputCommand, InputScopeVersion = 31 };
        var window = new AppWindow { Width = 470, Height = 260, Content = viewport };
        window.Show();
        try
        {
            window.UpdateLayout();
            viewport.Focus();
            var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
            viewport.RaiseEvent(request);
            if (policy == ApprovalPolicy.ReadOnly) Assert.Null(request.Client);
            else Assert.IsAssignableFrom<TextInputMethodClient>(request.Client).SetPreeditText("ni hao");
            Assert.False(input.IsBusy);
            Assert.False(HasRow(fixture.Store.Capture()!, "IME-RESULT:"));
            viewport.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });
            await WaitInputAsync(input);
            if (policy == ApprovalPolicy.FullAccess)
                await fixture.WaitFrameAsync(frame => HasRow(frame, "IME-RESULT:" + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(text))));
            else
            {
                Assert.False(input.CanInput);
                Assert.False(HasRow(fixture.Store.Capture()!, "IME-RESULT:"));
                if (policy == ApprovalPolicy.AskEveryTime) Assert.True(input.HasNotice);
            }
            Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static bool HasRow(TerminalFrame frame, string prefix) => frame.Rows.Any(row =>
        string.Concat(row.Select(cell => cell.Text)).StartsWith(prefix, StringComparison.Ordinal));

    private static async Task WaitInputAsync(TerminalInputController input)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (input.IsBusy) await Task.Delay(20, deadline.Token);
    }
}
