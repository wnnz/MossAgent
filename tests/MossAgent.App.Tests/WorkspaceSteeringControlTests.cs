using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSteeringControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 480, false)]
    [InlineData(true, 480, false)]
    [InlineData(false, 1000, false)]
    [InlineData(true, 1000, false)]
    [InlineData(false, 360, true)]
    [InlineData(true, 360, true)]
    public Task KeyboardAndSharedButtonAcceptIntoTheSameRunAndDisplayDurableBoundaryReceipts(bool dark, int width, bool project) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: project);
            var workspace = fixture.Workspace;
            var view = new WorkspaceComposerView { DataContext = workspace };
            var window = new AppWindow { Width = width, Height = 640, Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                workspace.PrimaryActionCommand.Execute(null);
                await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
                var taskId = workspace.Catalog.SelectedTask!.Id;
                workspace.ComposerText = string.Empty;
                Flush(window);
                var input = Assert.Single(view.GetVisualDescendants().OfType<AppComposerInput>());
                Assert.True(input.Focus(NavigationMethod.Tab));
                window.KeyTextInput("来自实际键盘的引导");
                Assert.Equal("来自实际键盘的引导", workspace.ComposerText);
                Press(window, Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, "\r");
                await fixture.WaitUntilAsync(() => workspace.Steering.History.Items.Count == 1 && !workspace.Steering.IsBusy);
                Assert.Equal(string.Empty, input.Text);
                Assert.Single(fixture.Server.Requests);
                Assert.Empty(workspace.Queue.Items);
                var first = workspace.Steering.History.Items[0];
                first.IsExpanded = true;
                Assert.Contains("已接收", first.Status);
                Assert.True(input.Focus(NavigationMethod.Tab));
                window.KeyTextInput("通过共享按钮继续引导");
                Flush(window);
                var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), control => control.Name == "SteerButton");
                Assert.True(button.IsEffectivelyVisible && button.IsEffectivelyEnabled);
                Assert.True(button.Focus(NavigationMethod.Tab));
                Press(window, Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                await fixture.WaitUntilAsync(() => workspace.Steering.History.Items.Count == 2 && !workspace.Steering.IsBusy);
                Flush(window);
                Assert.Same(workspace.Steering.SubmitCommand, button.Command);
                var origin = button.TranslatePoint(default, view)!.Value;
                Assert.InRange(origin.X, 0, view.Bounds.Width);
                Assert.InRange(origin.X + button.Bounds.Width, 0, view.Bounds.Width);
                Assert.InRange(origin.Y + button.Bounds.Height, 0, window.Bounds.Height);
                Assert.Same(workspace.ComposerRouter.SubmitCommand, view.FindControl<AppIconButton>("SendButton")!.Command);
                Capture(window, dark, width);
                workspace.ComposerText = "引导后的下一条草稿";
                fixture.Server.ReleaseResponses();
                await fixture.WaitUntilAsync(() => !workspace.IsRunning && !workspace.Steering.History.IsLoading
                    && workspace.Steering.History.Items.Count == 2 && workspace.Steering.History.Items.All(item => item.Status.Contains("本地模型输入", StringComparison.Ordinal)));
                Assert.Equal(2, fixture.Server.Requests.Count);
                Assert.Equal(taskId, workspace.Catalog.SelectedTask?.Id);
                Assert.Equal("引导后的下一条草稿", workspace.ComposerText);
                Assert.False(workspace.Steering.SubmitCommand.CanExecute(null));
                var receipts = (await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items;
                Assert.Equal(2, receipts.Count);
                Assert.All(receipts, receipt => Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, receipt.State));
                var history = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
                Assert.All(receipts, receipt => Assert.Single(history, message => message.Id == receipt.Id));
                Assert.Equal(receipts[0].RunId, receipts[1].RunId);
                Assert.Same(first, workspace.Steering.History.Items.Single(item => item.Id == first.Id));
                Assert.True(first.IsExpanded);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void Press(AppWindow window, Key key, RawInputModifiers modifiers, PhysicalKey physical, string? symbol)
    {
        window.KeyPress(key, modifiers, physical, symbol);
        window.KeyRelease(key, modifiers, physical, symbol);
        Dispatcher.UIThread.RunJobs();
    }
    private static void Flush(AppWindow window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void Capture(AppWindow window, bool dark, int width)
    {
        if (Environment.GetEnvironmentVariable("MOSSAGENT_STEERING_SCREENSHOTS") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(output, $"steering-{dark}-{width}.png"), PngBitmapEncoderOptions.Default);
    }
}
