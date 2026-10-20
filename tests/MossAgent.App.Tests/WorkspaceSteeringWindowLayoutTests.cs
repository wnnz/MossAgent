using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSteeringWindowLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 526, 601)]
    [InlineData(false, 1280, 820)]
    [InlineData(true, 1280, 820)]
    public Task FullWindowKeepsMessagesComposerAndSteeringReachableWithBothQueuesAndALongDraft(bool dark, int width, int height) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
            var workspace = fixture.Workspace;
            fixture.Main.IsRightPanelOpen = false;
            var window = new MainWindow { DataContext = fixture.Main, WindowState = WindowState.Normal,
                Width = width, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                workspace.PrimaryActionCommand.Execute(null);
                await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
                for (var index = 0; index < 4; index++)
                {
                    workspace.ComposerText = $"queued next run {index}";
                    await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
                }
                for (var index = 0; index < 2; index++)
                {
                    workspace.ComposerText = $"guide this run {index}";
                    await workspace.Steering.SubmitCommand.ExecuteAsync(null);
                }
                workspace.ComposerText = string.Join('\n', Enumerable.Repeat("long foreground draft stays editable", 14));
                Flush(window);
                var composer = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceComposerView>());
                var messages = window.GetVisualDescendants().OfType<AppScrollViewer>().Single(control => control.Name == "MessageScrollViewer");
                Capture(window, dark, width, height, "draft");
                Assert.True(messages.Bounds.Height >= 72, $"Message viewport {messages.Bounds}; composer {composer.Bounds}; visible {messages.IsEffectivelyVisible}; settings {fixture.Main.IsSettingsOpen}; conversation {fixture.Main.Pages.IsConversationOpen}; messages {workspace.Messages.Count}.");
                var send = composer.FindControl<AppIconButton>("SendButton")!;
                send.BringIntoView();
                Flush(window);
                AssertInside(window, send);
                Capture(window, dark, width, height, "stop");
                var steer = composer.GetVisualDescendants().OfType<AppButton>().Single(control => control.Name == "SteerButton");
                steer.BringIntoView();
                Flush(window);
                AssertInside(window, steer);
                Assert.True(steer.Focus());
                Capture(window, dark, width, height, "steer");
                workspace.PrimaryActionCommand.Execute(null);
                await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Queue.Items.Count == 4);
                Assert.Equal(4, workspace.Queue.Items.Count);
                Assert.Single(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void AssertInside(MainWindow window, Control control)
    {
        var origin = control.TranslatePoint(default, window)!.Value;
        Assert.InRange(origin.X, 0, window.ClientSize.Width);
        Assert.InRange(origin.Y, 0, window.ClientSize.Height);
        Assert.InRange(origin.X + control.Bounds.Width, 0, window.ClientSize.Width);
        Assert.InRange(origin.Y + control.Bounds.Height, 0, window.ClientSize.Height);
        foreach (var viewport in control.GetVisualAncestors().OfType<AppScrollViewer>())
        {
            var relative = control.TranslatePoint(default, viewport)!.Value;
            Assert.InRange(relative.Y, 0, viewport.Bounds.Height);
            Assert.InRange(relative.Y + control.Bounds.Height, 0, viewport.Bounds.Height);
        }
    }
    private static void Flush(MainWindow window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void Capture(MainWindow window, bool dark, int width, int height, string action)
    {
        if (Environment.GetEnvironmentVariable("MOSSAGENT_STEERING_SCREENSHOTS") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(output, $"steering-window-{dark}-{width}-{height}-{action}.png"), PngBitmapEncoderOptions.Default);
    }
}
