using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalPaneLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, 320, false)]
    [InlineData(470, 620, false)]
    [InlineData(280, 320, true)]
    [InlineData(470, 620, true)]
    public Task BusyCancelAndTruncationKeepControlsAlignedAndInsidePane(int width, int height, bool dark) => _session.DispatchAsync(async () =>
    {
        var source = new InspectorProbeTerminal();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task });
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        model.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var pane = new AppTerminalPane { DataContext = model };
        var window = new AppWindow { Width = width, Height = height, Content = pane, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        var starting = model.StartCommand.ExecuteAsync(null);
        try
        {
            source.Emit(context.TaskId, new string('x', 210_000));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var buttons = pane.GetVisualDescendants().OfType<AppButton>().ToArray();
            var start = Assert.Single(buttons, item => Equals(item.Content, InspectorText.TerminalStart));
            var cancel = Assert.Single(buttons, item => Equals(item.Content, InspectorText.TerminalCancel));
            var stop = Assert.Single(buttons, item => Equals(item.Content, InspectorText.TerminalStop));
            Assert.False(start.IsEffectivelyVisible);
            Assert.True(cancel.IsEffectivelyVisible);
            Assert.True(cancel.IsEffectivelyEnabled);
            Assert.False(stop.IsEffectivelyEnabled);
            Assert.True(Avalonia.Application.Current!.Resources.TryGetResource("ButtonBackgroundBrush", window.ActualThemeVariant, out var background));
            Assert.True(Avalonia.Application.Current.Resources.TryGetResource("TextSubtleBrush", window.ActualThemeVariant, out var foreground));
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(background).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(stop.Background).Color);
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(foreground).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(stop.Foreground).Color);
            Assert.Equal(cancel.TranslatePoint(new Point(0, cancel.Bounds.Height / 2), pane)!.Value.Y,
                stop.TranslatePoint(new Point(0, stop.Bounds.Height / 2), pane)!.Value.Y);
            var notice = Assert.Single(pane.GetVisualDescendants().OfType<AppText>(), item => item.Text == InspectorText.TerminalTruncated);
            Assert.True(notice.IsEffectivelyVisible);
            foreach (var button in buttons.Where(item => item.IsEffectivelyVisible))
            {
                var end = button.TranslatePoint(new Point(button.Bounds.Width, button.Bounds.Height), pane)!.Value;
                Assert.InRange(end.X, 0, width);
                Assert.InRange(end.Y, 0, height);
            }
            cancel.Command!.Execute(null);
            Assert.False(cancel.IsEffectivelyEnabled);
            Assert.True(model.IsBusy);
            Assert.Contains("取消中", cancel.Content!.ToString());
            pending.SetResult(ToolResult.Failure("cancelled", "tool_cancelled"));
            await starting;
            window.UpdateLayout();
            Assert.True(start.IsEffectivelyVisible);
            Assert.False(cancel.IsEffectivelyVisible);
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await starting; window.Close(); }
    }, TestContext.Current.CancellationToken);
}
