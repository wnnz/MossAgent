using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceFollowupControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 480, 600, false)]
    [InlineData(true, 480, 600, false)]
    [InlineData(false, 1000, 800, false)]
    [InlineData(true, 1000, 800, false)]
    [InlineData(false, 360, 600, true)]
    [InlineData(true, 360, 600, true)]
    public Task EnterQueuesWhileStreamingAndTheStopAndQueueControlsRemainSeparate(bool dark, int width, int height, bool withProject) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: withProject);
            var workspace = fixture.Workspace;
            var view = new WorkspaceComposerView { DataContext = workspace };
            var window = new AppWindow { Width = width, Height = height, Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                workspace.PrimaryActionCommand.Execute(null);
                await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
                workspace.ComposerText = "用键盘加入下一轮队列";
                Flush(window);
                var input = Assert.Single(view.GetVisualDescendants().OfType<AppComposerInput>());
                Assert.True(input.IsSubmissionEnabled);
                Assert.True(input.Focus());
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                await fixture.WaitUntilAsync(() => workspace.Queue.Items.Count == 1 && !workspace.Queue.IsBusy);
                Flush(window);
                Assert.Single(fixture.Server.Requests);
                Assert.True(workspace.HasActiveRun);
                Assert.Equal(string.Empty, input.Text);
                var stop = view.FindControl<AppIconButton>("StopButton")!;
                var queue = view.FindControl<AppButton>("QueueButton")!;
                Assert.True(stop.IsEffectivelyVisible && stop.IsEffectivelyEnabled);
                Assert.True(stop.IsFilledIcon);
                Assert.True(queue.IsEffectivelyVisible);
                Assert.Same(workspace.PrimaryActionCommand, stop.Command);
                Assert.Same(workspace.ComposerRouter.SubmitCommand, queue.Command);
                var queueOrigin = queue.TranslatePoint(default, view)!.Value;
                var stopOrigin = stop.TranslatePoint(default, view)!.Value;
                Assert.InRange(queueOrigin.X + queue.Bounds.Width, 0, stopOrigin.X);
                Assert.InRange(stopOrigin.X + stop.Bounds.Width, 0, view.Bounds.Width);
                workspace.Queue.Items[0].EditCommand.Execute(null);
                Flush(window);
                var editor = Assert.Single(view.GetVisualDescendants().OfType<AppInput>(), control => control.Classes.Contains("followup-editor"));
                Assert.True(editor.IsEffectivelyVisible);
                editor.Text = "通过实际编辑控件保存";
                if (Environment.GetEnvironmentVariable("MOSSAGENT_QUEUE_SCREENSHOTS") is { Length: > 0 } output)
                {
                    Directory.CreateDirectory(output);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using var frame = window.CaptureRenderedFrame();
                    Assert.NotNull(frame);
                    frame.Save(Path.Combine(output, $"queue-{dark}-{width}-{height}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                await workspace.Queue.SaveEditCommand.ExecuteAsync(null);
                Assert.Equal("通过实际编辑控件保存", Assert.Single(workspace.Queue.Items).Prompt);
                fixture.Server.ReleaseResponses();
                await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
                Assert.Empty(workspace.Queue.Items);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void Flush(AppWindow window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
}
