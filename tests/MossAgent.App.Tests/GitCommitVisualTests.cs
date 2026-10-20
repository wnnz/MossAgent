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
public sealed class GitCommitVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280)]
    [InlineData(true, 280)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task ConfirmationBusyCancellationAndFocusStayWithinTheSharedDialog(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, token) =>
        {
            if (request.ToolName == "git.commit_preview") return Task.FromResult(ToolResult.Success("preview", JsonSerializer.Serialize(
                new GitCommitPreview(new string('a', 64), 3, new string('b', 40), "refs/heads/feature/dialog"), JsonSerializerOptions.Web)));
            operation = token; return pending.Task;
        } };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask);
        model.Attach(new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess));
        var actions = new GitCommitActionsView { DataContext = model };
        var dialog = new GitCommitDialog { DataContext = model };
        var window = new AppWindow { Content = new Grid { Children = { actions, dialog } }, Width = width, Height = 560,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var open = actions.FindControl<AppButton>("OpenCommitButton")!;
            Activate(open);
            await model.OpenCommand.ExecutionTask!;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var input = dialog.FindControl<AppInput>("CommitMessageInput")!;
            var confirm = dialog.FindControl<AppButton>("ConfirmCommitButton")!;
            Assert.False(confirm.IsEffectivelyEnabled);
            Assert.True(input.IsFocused);
            input.Text = "统一提交界面\n\n保留多行说明。";
            window.UpdateLayout();
            Assert.True(confirm.IsEffectivelyEnabled);
            AssertWithin(input, window);
            AssertWithin(confirm, window);
            Save(window, dark, width, "ready");
            Activate(confirm);
            window.UpdateLayout();
            Assert.True(model.IsBusy);
            Assert.False(input.IsEffectivelyEnabled);
            Assert.False(model.CloseCommand.CanExecute(null));
            var stop = dialog.FindControl<AppButton>("StopCommitButton")!;
            Assert.True(stop.IsEffectivelyEnabled);
            Activate(stop);
            Assert.True(operation.IsCancellationRequested);
            pending.SetResult(ToolResult.Success("possibly committed"));
            await model.CommitCommand.ExecutionTask!;
            Assert.True(model.IsOpen);
            Assert.Equal("统一提交界面\n\n保留多行说明。", model.Message);
            Assert.False(model.HasPreview);
            window.UpdateLayout();
            Save(window, dark, width, "interrupted");
            input.Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(model.IsOpen);
            Assert.True(open.IsFocused);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertWithin(Control control, Control window)
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

    private static void Save(AppWindow window, bool dark, int width, string state)
    {
        if (Environment.GetEnvironmentVariable("MOSSAGENT_GIT_COMMIT_VISUAL_OUTPUT") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, 560), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(output, $"commit-{(dark ? "dark" : "light")}-{width}-{state}.png"), PngBitmapEncoderOptions.Default);
    }
}
