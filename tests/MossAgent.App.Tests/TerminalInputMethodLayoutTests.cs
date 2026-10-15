using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalInputMethodLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, false)]
    [InlineData(470, false)]
    [InlineData(280, true)]
    [InlineData(470, true)]
    public Task CompositionIsLocalUntilOneCommittedTextRequest(int width, bool dark) => WithViewport(width, dark, (window, viewport, requests) =>
    {
        var client = RequestClient(viewport);
        Assert.Same(viewport, client.TextViewVisual);
        Assert.True(client.SupportsPreedit);
        Assert.False(client.SupportsSurroundingText);
        Assert.Empty(client.SurroundingText);
        Assert.Equal(new TextSelection(0, 0), client.Selection);
        client.SetPreeditText("ni hao", 3);
        window.UpdateLayout();
        var preedit = viewport.FindControl<AppText>("TerminalPreedit")!;
        Assert.Equal("ni hao", preedit.Text);
        Assert.True(preedit.IsEffectivelyVisible);
        Assert.Empty(requests);
        AssertInside(client.CursorRectangle, viewport);
        viewport.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "你好" });
        Assert.Equal("你好", Assert.Single(requests).Text);
        Assert.Equal(viewport.Frame!.SessionId, requests[0].SessionId);
        Assert.Equal(13, requests[0].ScopeGeneration);
        Assert.False(preedit.IsVisible);
    });

    [Theory]
    [InlineData("session")]
    [InlineData("scope")]
    [InlineData("command")]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("detached")]
    [InlineData("alternate")]
    [InlineData("focus")]
    [InlineData("view")]
    [InlineData("task")]
    [InlineData("failed")]
    [InlineData("ancestor_hidden")]
    [InlineData("ancestor_disabled")]
    public Task OwnershipChangesClearAndPermanentlyRejectOldComposition(string change) => WithViewport(470, false, (window, viewport, requests) =>
    {
        var client = RequestClient(viewport);
        var resets = 0;
        client.ResetRequested += (_, _) => resets++;
        client.SetPreeditText("未确认");
        if (change == "session") viewport.Frame = viewport.Frame! with { SessionId = Guid.NewGuid() };
        if (change == "scope") viewport.InputScopeVersion++;
        if (change == "command") viewport.InputCommand = new RelayCommand<TerminalInputRequest>(_ => { });
        if (change == "hidden") { viewport.IsVisible = false; viewport.IsVisible = true; }
        if (change == "disabled") { viewport.IsEnabled = false; viewport.IsEnabled = true; }
        if (change == "detached") { var content = window.Content; window.Content = null; window.Content = content; }
        if (change == "alternate") viewport.Frame = viewport.Frame! with { AlternateScreen = true };
        if (change == "view") viewport.Frame = viewport.Frame! with { ViewId = Guid.NewGuid() };
        if (change == "task") viewport.Frame = viewport.Frame! with { TaskId = Guid.NewGuid() };
        if (change == "failed") viewport.Frame = viewport.Frame! with { Failed = true };
        if (change == "ancestor_hidden") { ((Grid)window.Content!).IsVisible = false; ((Grid)window.Content!).IsVisible = true; }
        if (change == "ancestor_disabled") { ((Grid)window.Content!).IsEnabled = false; ((Grid)window.Content!).IsEnabled = true; }
        if (change == "focus") Assert.Single(((Grid)window.Content!).Children.OfType<AppButton>()).Focus();
        viewport.Focus();
        client.SetPreeditText("迟到内容");
        window.UpdateLayout();
        Assert.True(resets > 0);
        Assert.False(viewport.FindControl<AppText>("TerminalPreedit")!.IsVisible);
        Assert.Empty(requests);
        if (change == "failed") { Assert.Null(ClientOrNull(viewport)); return; }
        var replacement = RequestClient(viewport);
        Assert.NotSame(client, replacement);
        replacement.SetPreeditText("新候选");
        client.SetPreeditText("更迟的内容");
        Assert.Equal("新候选", viewport.FindControl<AppText>("TerminalPreedit")!.Text);
    });

    [Fact]
    public Task MissingOrRejectedInputCommandCannotAcquireComposition() => WithViewport(470, false, (window, viewport, requests) =>
    {
        viewport.InputCommand = null;
        Assert.Null(ClientOrNull(viewport));
        viewport.InputCommand = new RelayCommand<TerminalInputRequest>(_ => { }, _ => false);
        Assert.Null(ClientOrNull(viewport));
        Assert.Empty(requests);
    });

    [Fact]
    public Task CompositionKeysDoNotReachTheProcessAndUnicodeCaretIsBounded() => WithViewport(280, true, (window, viewport, requests) =>
    {
        var client = RequestClient(viewport);
        client.SetPreeditText("中🚀e\u0301", 2);
        window.UpdateLayout();
        AssertInside(client.CursorRectangle, viewport);
        foreach (var key in new[] { Key.Enter, Key.Escape, Key.Left, Key.Back })
            viewport.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Assert.Empty(requests);
        client.SetPreeditText(null);
        viewport.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(Key.Enter, Assert.Single(requests).Key);
    });

    [Fact]
    public Task OversizedOrControlPreeditIsResetWithoutAProcessWrite() => WithViewport(470, false, (window, viewport, requests) =>
    {
        var client = RequestClient(viewport);
        var resets = 0;
        client.ResetRequested += (_, _) => resets++;
        foreach (var text in new[] { new string('x', 1025), "line\nnext", "\u001b[31m", "\u0085" })
        {
            client.SetPreeditText(text);
            Assert.False(viewport.FindControl<AppText>("TerminalPreedit")!.IsVisible);
        }
        Assert.Equal(4, resets);
        Assert.Empty(requests);
        client.SetPreeditText("恢复");
        Assert.Equal("恢复", viewport.FindControl<AppText>("TerminalPreedit")!.Text);
    });

    [Fact]
    public Task CommandRejectionResetsCompositionAndReenablingRequestsFreshClient() => WithViewport(470, false, (window, viewport, requests) =>
    {
        var enabled = true;
        var command = new RelayCommand<TerminalInputRequest>(request => requests.Add(request!), _ => enabled);
        viewport.InputCommand = command;
        var client = RequestClient(viewport);
        client.SetPreeditText("未确认");
        enabled = false;
        command.NotifyCanExecuteChanged();
        Assert.Null(ClientOrNull(viewport));
        Assert.False(viewport.FindControl<AppText>("TerminalPreedit")!.IsVisible);
        var requeries = 0;
        InputMethod.AddTextInputMethodClientRequeryRequestedHandler(viewport, (_, _) => requeries++);
        enabled = true;
        command.NotifyCanExecuteChanged();
        Assert.True(requeries > 0);
        client.SetPreeditText("迟到内容");
        Assert.False(viewport.FindControl<AppText>("TerminalPreedit")!.IsVisible);
        Assert.NotSame(client, RequestClient(viewport));
        Assert.Empty(requests);
    });

    [Theory]
    [InlineData(210)]
    [InlineData(470)]
    public Task ScrollingAndNewFramesMoveCandidateAnchorWithoutReplacingItsClient(int resizedWidth) => WithViewport(280, false, (window, viewport, requests) =>
    {
        using var screen = new TerminalVtScreen();
        screen.Process(string.Concat(Enumerable.Repeat("long scrolling output\r\n", 45)) + "prompt> ");
        var previous = viewport.Frame!;
        viewport.Frame = screen.Capture(previous.ViewId, previous.TaskId, previous.SessionId, false);
        window.UpdateLayout();
        var client = RequestClient(viewport);
        var changes = 0;
        client.CursorRectangleChanged += (_, _) => changes++;
        client.SetPreeditText("候选词", 1);
        window.UpdateLayout();
        var scroller = viewport.FindControl<AppScrollViewer>("TerminalScroller")!;
        scroller.Offset = default;
        window.UpdateLayout();
        AssertInside(client.CursorRectangle, viewport);
        Assert.True(changes > 0);
        screen.Process("after");
        viewport.Frame = screen.Capture(previous.ViewId, previous.TaskId, previous.SessionId, false);
        window.UpdateLayout();
        window.Width = resizedWidth;
        window.UpdateLayout();
        AssertInside(client.CursorRectangle, viewport);
        Assert.Same(client, RequestClient(viewport));
        Assert.Equal("候选词", viewport.FindControl<AppText>("TerminalPreedit")!.Text);
        Assert.Empty(requests);
    });

    private Task WithViewport(double width, bool dark, Action<AppWindow, AppTerminalViewport, List<TerminalInputRequest>> check) => _session.Dispatch(() =>
    {
        using var source = new TerminalVtScreen();
        source.Process("PS synthetic> ");
        var requests = new List<TerminalInputRequest>();
        var viewport = new AppTerminalViewport
        {
            Frame = source.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false),
            InputScopeVersion = 13,
            InputCommand = new RelayCommand<TerminalInputRequest>(request => requests.Add(request!))
        };
        var window = new AppWindow
        {
            Width = width,
            Height = 260,
            Content = new Grid
            {
                Children =
                {
                    viewport,
                    new AppButton
                    {
                        Content = "焦点",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
                    }
                }
            },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); viewport.Focus(); check(window, viewport, requests); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static TextInputMethodClient RequestClient(AppTerminalViewport viewport) => Assert.IsAssignableFrom<TextInputMethodClient>(ClientOrNull(viewport));

    private static TextInputMethodClient? ClientOrNull(AppTerminalViewport viewport)
    {
        var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        viewport.RaiseEvent(request);
        return request.Client;
    }

    private static void AssertInside(Rect rectangle, Control viewport)
    {
        Assert.InRange(rectangle.X, 0, viewport.Bounds.Width);
        Assert.InRange(rectangle.Y, 0, viewport.Bounds.Height);
        Assert.InRange(rectangle.Right, rectangle.Left, viewport.Bounds.Width);
        Assert.InRange(rectangle.Bottom, rectangle.Top, viewport.Bounds.Height);
        Assert.True(rectangle.Height > 0);
    }
}
