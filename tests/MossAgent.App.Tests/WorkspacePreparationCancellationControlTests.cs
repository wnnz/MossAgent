using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspacePreparationCancellationControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526)]
    [InlineData(false, 1000)]
    [InlineData(true, 526)]
    [InlineData(true, 1000)]
    public Task PreparingSend_UsesAKeyboardOperableStopButtonAndDisablesRepeatedCancellation(bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => { entered.TrySetResult(); return release.Task; } };
            using var runs = new WorkspaceRunTracker();
            await using var workspace = fixture.CreateWorkspace(repository, runs);
            await workspace.LoadAsync();
            workspace.ComposerText = "取消准备，不请求模型。";
            var view = new WorkspaceComposerView { DataContext = workspace };
            var window = new AppWindow { Width = width, Height = 260, Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                workspace.PrimaryActionCommand.Execute(null);
                await entered.Task.WaitAsync(fixture.Token);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var stop = view.FindControl<AppIconButton>("StopButton")!;
                Assert.True(stop.IsEffectivelyVisible);
                Assert.True(stop.IsEffectivelyEnabled);
                Assert.Same(workspace.PrimaryActionCommand, stop.Command);
                Assert.True(stop.IsFilledIcon);
                Assert.Equal("取消发送准备", stop.Label);
                SaveEvidence(window, dark, width, "preparing");
                Assert.True(stop.Focus());
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(workspace.IsRunning);
                Assert.False(stop.IsEffectivelyEnabled);
                Assert.Equal("正在取消发送准备", stop.Label);
                SaveEvidence(window, dark, width, "cancelling");
                release.TrySetResult();
                await fixture.WaitUntilAsync(() => !workspace.IsRunning);
                Dispatcher.UIThread.RunJobs();
                var send = view.FindControl<AppIconButton>("SendButton")!;
                Assert.False(send.IsAlternateIcon);
                Assert.True(send.IsEffectivelyEnabled);
                Assert.Equal("发送消息", send.Label);
                Assert.Equal("取消准备，不请求模型。", workspace.ComposerText);
                Assert.Empty(fixture.Server.Requests);
            }
            finally { release.TrySetResult(); window.Close(); await workspace.ShutdownAsync(); }
        }, TestContext.Current.CancellationToken);

    private static void SaveEvidence(AppWindow window, bool dark, int width, string phase)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_PREPARATION_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{phase}-dark-{dark}-width-{width}.png"), PngBitmapEncoderOptions.Default);
    }
}
