using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Terminal;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalInteractionLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, false)]
    [InlineData(470, false)]
    [InlineData(280, true)]
    [InlineData(470, true)]
    public Task AutomaticGeometrySettlesWithoutPendingNoticeFeedback(int width, bool dark) => _session.DispatchAsync(async () =>
    {
        var source = new TerminalStateProbe();
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        source.Emit(new(context.TaskId, Guid.NewGuid(), 1, TerminalSessionState.Running, Grid: TerminalGridSize.Default));
        var requests = 0;
        using var model = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) =>
            {
                Assert.Equal("terminal.resize", request.ToolName);
                requests++;
                var state = source.GetSnapshot(context.TaskId)!;
                source.Emit(state with
                {
                    Revision = state.Revision + 1,
                    Grid = new(request.Arguments.GetProperty("columns").GetInt32(), request.Arguments.GetProperty("rows").GetInt32())
                });
                return Task.FromResult(ToolResult.Success("ok"));
            }
        });
        model.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var pane = new AppTerminalPane { DataContext = model };
        var window = new AppWindow { Width = width, Height = 620, Content = pane, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            source.EmitOutput(context.TaskId, "PS synthetic> ready\r\n");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                while (requests == 0 || model.Resize.IsBusy || model.Frame?.Columns == 120)
                { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); await Task.Delay(20, deadline.Token); }
            }
            catch (OperationCanceledException)
            { Assert.Fail($"requests={requests}, busy={model.Resize.IsBusy}, notice={model.Resize.Notice}, grid={source.GetSnapshot(context.TaskId)?.Grid}, frame={model.Frame?.Columns}x{model.Frame?.ViewportRows}, failed={model.Frame?.Failed}, output={model.HasOutput}, resize={model.Resize.CanResize}"); }
            var stable = requests;
            var settledSince = Environment.TickCount64;
            while (Environment.TickCount64 - settledSince < 600)
            {
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(20, deadline.Token);
                var grid = source.GetSnapshot(context.TaskId)!.Grid!;
                if (requests != stable || model.Resize.IsBusy || model.Frame!.Columns != grid.Columns || model.Frame.ViewportRows != grid.Rows)
                { stable = requests; settledSince = Environment.TickCount64; }
            }
            Assert.InRange(requests, 1, 3);
            Assert.Equal(stable, requests);
            Assert.False(model.Resize.HasNotice);
            var viewport = Assert.Single(pane.GetVisualDescendants().OfType<AppTerminalViewport>());
            var screen = Assert.Single(viewport.GetVisualDescendants().OfType<AppTerminalScreen>());
            var scroll = Assert.Single(viewport.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.True(screen.Bounds.Width <= scroll.Viewport.Width + 1 || model.Frame!.Columns == 20 && scroll.Viewport.Width < 20 * screen.CellWidth,
                $"width={width}, frame={model.Frame?.Columns}, cell={screen.CellWidth}, screen={screen.Bounds}, viewport={scroll.Viewport}, pane={pane.Bounds}");
            Assert.True(model.Frame!.ViewportRows * screen.CellHeight <= scroll.Viewport.Height + 1,
                $"requests={requests}, frame={model.Frame.Columns}x{model.Frame.ViewportRows}, cell={screen.CellWidth}x{screen.CellHeight}, viewport={scroll.Viewport}, grid={source.GetSnapshot(context.TaskId)?.Grid}");
            Assert.True(viewport.Focusable);
            Assert.Same(model.Keyboard.InputCommand, viewport.InputCommand);
            Assert.Equal(0, source.DirectCalls);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FocusedViewportRoutesSpecialKeysAndComposedTextWithoutDuplicates() => _session.Dispatch(() =>
    {
        using var source = new TerminalVtScreen();
        source.Process("PS synthetic> ");
        var frame = source.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false);
        var requests = new List<TerminalInputRequest>();
        var viewport = new AppTerminalViewport { Frame = frame, InputCommand = new RelayCommand<TerminalInputRequest>(request => requests.Add(request!)) };
        var window = new AppWindow { Width = 470, Height = 300, Content = viewport };
        window.Show();
        try
        {
            window.UpdateLayout();
            viewport.Focus();
            var screen = Assert.Single(viewport.GetVisualDescendants().OfType<AppTerminalScreen>());
            var letter = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.A };
            screen.RaiseEvent(letter);
            Assert.False(letter.Handled);
            var text = new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "a红" };
            screen.RaiseEvent(text);
            Assert.True(text.Handled);
            var arrow = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left };
            screen.RaiseEvent(arrow);
            Assert.True(arrow.Handled);
            Assert.Equal(2, requests.Count);
            Assert.Equal("a红", requests[0].Text);
            Assert.Equal(Key.Left, requests[1].Key);
            Assert.All(requests, request => { Assert.Equal(frame.ViewId, request.ViewId); Assert.Equal(frame.SessionId, request.SessionId); });
            viewport.IsVisible = false;
            screen.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "hidden" });
            Assert.Equal(2, requests.Count);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
