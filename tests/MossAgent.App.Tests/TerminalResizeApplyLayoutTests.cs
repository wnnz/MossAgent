using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
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
public sealed class TerminalResizeApplyLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ManualResizeRowFitsNarrowPane(bool dark) => _session.Dispatch(() =>
    {
        var source = new InspectorProbeTerminal();
        using var model = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.AskEveryTime);
        model.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var pane = new AppTerminalPane { DataContext = model };
        var window = new AppWindow { Width = 280, Height = 320, Content = pane, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            SaveFrame(window, dark);
            Assert.True(model.Resize.RequiresExplicitResize);
            var apply = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(),
                button => Equals(button.Content, InspectorText.TerminalResizeApply) && button.IsEffectivelyVisible);
            var hint = Assert.Single(pane.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == InspectorText.TerminalResizeManualHint && text.IsEffectivelyVisible);
            Assert.False(apply.IsEffectivelyEnabled);
            var end = apply.TranslatePoint(new Point(apply.Bounds.Width, apply.Bounds.Height), pane)!.Value;
            Assert.InRange(end.X, 0, 280);
            Assert.InRange(end.Y, 0, 320);
            var hintEnd = hint.TranslatePoint(new Point(hint.Bounds.Width, hint.Bounds.Height), pane)!.Value;
            Assert.InRange(hintEnd.X, 0, 280);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void SaveFrame(AppWindow window, bool dark)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_TEST_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"terminal-manual-{(dark ? "dark" : "light")}.png"), PngBitmapEncoderOptions.Default);
    }
}
