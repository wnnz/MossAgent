using System.Text.Json;
using Avalonia;
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
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitPushVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280, 560)]
    [InlineData(true, 280, 560)]
    [InlineData(false, 640, 560)]
    [InlineData(true, 640, 560)]
    [InlineData(false, 280, 700)]
    [InlineData(true, 280, 700)]
    [InlineData(false, 640, 700)]
    [InlineData(true, 640, 700)]
    public Task SharedPushDialogSupportsKeyboardCancellationRecoveryAndBothThemes(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, token) =>
        {
            if (request.ToolName == "git.push_preview") return Task.FromResult(ToolResult.Success("preview", JsonSerializer.Serialize(
                new GitPushPreview(new string('a', 64), new string('b', 40), "feature/source", "origin", "feature/review",
                    "https://example.invalid", ["origin", "upstream"], null), JsonSerializerOptions.Web)));
            operation = token; return pending.Task;
        } };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess));
        var actions = new GitPushActionsView { DataContext = model };
        var dialog = new GitPushDialog { DataContext = model };
        var window = new AppWindow { Content = new Avalonia.Controls.Grid { Children = { actions, dialog } }, Width = width, Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var open = actions.FindControl<AppButton>("OpenPushButton")!;
            Activate(open);
            await model.OpenCommand.ExecutionTask!;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var remote = dialog.FindControl<AppInput>("PushRemoteInput")!;
            var branch = dialog.FindControl<AppInput>("PushBranchInput")!;
            var confirm = dialog.FindControl<AppButton>("ConfirmPushButton")!;
            Assert.True(remote.IsFocused);
            Assert.True(confirm.IsEffectivelyEnabled);
            AssertWithin(remote, window);
            AssertWithin(branch, window);
            AssertWithin(confirm, window);
            Save(window, dark, width, height, "ready");
            Activate(confirm);
            window.UpdateLayout();
            Assert.True(model.IsBusy);
            Assert.False(remote.IsEffectivelyEnabled);
            Assert.False(branch.IsEffectivelyEnabled);
            Assert.False(model.CloseCommand.CanExecute(null));
            var stop = dialog.FindControl<AppButton>("StopPushButton")!;
            AssertWithin(stop, window);
            Save(window, dark, width, height, "busy");
            Activate(stop);
            Assert.True(operation.IsCancellationRequested);
            pending.TrySetResult(ToolResult.Success("possibly pushed"));
            await model.PushCommand.ExecutionTask!;
            Assert.True(model.IsOpen);
            Assert.False(model.HasPreview);
            Assert.Equal("feature/review", branch.Text);
            window.UpdateLayout();
            Save(window, dark, width, height, "interrupted");
            await model.RefreshPreviewCommand.ExecuteAsync(null);
            pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var failed = model.PushCommand.ExecuteAsync(null);
            pending.SetResult(ToolResult.Failure("private-value", "git_push_failed"));
            await failed;
            window.UpdateLayout();
            Assert.False(confirm.IsEffectivelyEnabled);
            Save(window, dark, width, height, "failed");
            remote.Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(model.IsOpen);
            Assert.True(open.IsFocused);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertWithin(Avalonia.Controls.Control control, Avalonia.Controls.Control window)
    {
        var position = control.TranslatePoint(default, window)!.Value;
        Assert.InRange(position.X, 0, window.Bounds.Width);
        Assert.InRange(position.Y, 0, window.Bounds.Height);
        Assert.True(position.X + control.Bounds.Width <= window.Bounds.Width + 1);
        Assert.True(position.Y + control.Bounds.Height <= window.Bounds.Height + 1);
    }

    private static void Activate(AppButton button)
    {
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    private static void Save(AppWindow window, bool dark, int width, int height, string state)
    {
        if (Environment.GetEnvironmentVariable("MOSSAGENT_GIT_PUSH_VISUAL_OUTPUT") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(output, $"push-{(dark ? "dark" : "light")}-{width}x{height}-{state}.png"), PngBitmapEncoderOptions.Default);
    }
}
