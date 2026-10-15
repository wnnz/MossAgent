using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Threading;
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
public sealed class TerminalClipboardLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DragSelectionUsesWholeWideCellsAndCopyShortcutDoesNotWriteToProcess(bool dark) => _session.DispatchAsync(async () =>
    {
        using var source = new TerminalVtScreen();
        source.Process("A中e\u0301🚀Z");
        var requests = new List<TerminalInputRequest>();
        var viewport = new AppTerminalViewport
        {
            Frame = source.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false),
            InputCommand = new RelayCommand<TerminalInputRequest>(request => requests.Add(request!))
        };
        var window = new AppWindow { Width = 470, Height = 260, Content = viewport, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var screen = Assert.Single(viewport.GetVisualDescendants().OfType<AppTerminalScreen>());
            var start = screen.TranslatePoint(new Point(screen.CellWidth * 2.5, 9), window)!.Value;
            var end = screen.TranslatePoint(new Point(screen.CellWidth * 5.5, 9), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);
            Assert.Equal("中e\u0301🚀", screen.Selection.GetText());
            Assert.True(viewport.IsFocused);
            Assert.NotNull(screen.SelectionBrush);
            var copy = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift };
            screen.RaiseEvent(copy);
            Assert.True(copy.Handled);
            if (viewport.ClipboardActions.CopyCommand.ExecutionTask is { } operation) await operation;
            Assert.Equal("中e\u0301🚀", await window.Clipboard!.TryGetTextAsync());
            Assert.Empty(requests);
            screen.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Assert.False(screen.Selection.HasSelection);
            screen.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = KeyModifiers.Control });
            Assert.Equal(Key.C, Assert.Single(requests).Key);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PasteShortcutStagesMultilineAndHiddenParentCannotConfirm() => _session.DispatchAsync(async () =>
    {
        using var source = new TerminalVtScreen();
        source.Process("PS synthetic> ");
        var requests = new List<TerminalInputRequest>();
        var viewport = new AppTerminalViewport
        {
            Frame = source.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false),
            InputCommand = new RelayCommand<TerminalInputRequest>(request => requests.Add(request!))
        };
        var parent = new AppBorder { Child = viewport };
        var window = new AppWindow { Width = 470, Height = 260, Content = parent };
        window.Show();
        try
        {
            window.UpdateLayout();
            await window.Clipboard!.SetTextAsync("first\r\nsecond");
            var paste = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift };
            viewport.RaiseEvent(paste);
            Assert.True(paste.Handled);
            if (viewport.ClipboardActions.PasteCommand.ExecutionTask is { } operation) await operation;
            Assert.True(viewport.ClipboardActions.HasPendingPaste);
            Assert.Empty(requests);
            parent.IsVisible = false;
            parent.IsVisible = true;
            viewport.ClipboardActions.ConfirmPasteCommand.Execute(null);
            Assert.False(viewport.ClipboardActions.HasPendingPaste);
            Assert.Empty(requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SharedMenuBindsRealClipboardCommandsAndClosesOnSameSessionScopeChange() => _session.Dispatch(() =>
    {
        var source = new TerminalStateProbe();
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        source.Emit(new(context.TaskId, Guid.NewGuid(), 1, TerminalSessionState.Running, Grid: TerminalGridSize.Default));
        using var model = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        model.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var pane = new AppTerminalPane { DataContext = model };
        var window = new AppWindow { Width = 470, Height = 300, Content = pane };
        window.Show();
        try
        {
            source.EmitOutput(context.TaskId, "ready");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var viewport = pane.FindControl<AppTerminalViewport>("TerminalViewport")!;
            var button = pane.FindControl<AppIconButton>("TerminalMenuButton")!;
            var flyout = Assert.IsType<AppFlyout>(button.Flyout);
            flyout.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            var options = Assert.IsType<AppCard>(flyout.Content).GetVisualDescendants().OfType<AppButton>().ToArray();
            Assert.Equal(3, options.Length);
            Assert.Same(viewport.ClipboardActions.CopyCommand, options[0].Command);
            Assert.Same(viewport.ClipboardActions.PasteCommand, options[1].Command);
            Assert.Same(viewport.ClipboardActions.SelectAllCommand, options[2].Command);
            Assert.False(options[0].IsEffectivelyEnabled);
            viewport.ClipboardActions.SelectAllCommand.Execute(null);
            Assert.True(options[0].IsEffectivelyEnabled);
            model.AttachTask(context.TaskId, context.WorkingDirectory, context);
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
