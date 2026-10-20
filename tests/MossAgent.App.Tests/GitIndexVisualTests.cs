using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitIndexVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280)]
    [InlineData(true, 280)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task BusyCancelAndInterruptedStatesStayVisibleAndKeyboardAccessible(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, token) => { operation = token; return pending.Task; } };
        await using var actions = new GitIndexActionsViewModel(executor, _ => Task.CompletedTask);
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        actions.Attach(context);
        actions.Select(new("selected.txt", " M", false, true), false);
        var view = new GitIndexActionsView { DataContext = actions };
        var window = new AppWindow
        {
            Content = new AppBorder { Padding = new Thickness(12), Child = view }, Width = width, Height = 180,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var action = view.FindControl<AppButton>("IndexActionButton")!;
            var cancel = view.FindControl<AppButton>("IndexCancelButton")!;
            Assert.True(action.IsEffectivelyEnabled);
            Assert.False(cancel.IsEffectivelyVisible);
            Activate(action);
            window.UpdateLayout();
            Assert.True(actions.IsBusy);
            Assert.False(action.IsEffectivelyEnabled);
            Assert.True(cancel.IsEffectivelyVisible);
            Assert.True(cancel.IsEffectivelyEnabled);
            AssertWithin(action, view);
            AssertWithin(cancel, view);
            Save(window, dark, width, "busy");
            Activate(cancel);
            Assert.True(operation.IsCancellationRequested);
            pending.SetResult(ToolResult.Success("possibly applied"));
            await actions.ExecuteCommand.ExecutionTask!;
            await WaitUntilAsync(() => action.IsEffectivelyEnabled);
            window.UpdateLayout();
            Assert.True(actions.HasStatus);
            Assert.False(cancel.IsEffectivelyVisible);
            Assert.True(action.IsEffectivelyEnabled);
            var status = view.FindControl<AppText>("IndexStatus")!;
            AssertWithin(status, view);
            Assert.True(status.Bounds.Height > 0);
            Save(window, dark, width, "interrupted");
            actions.Attach(context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
            actions.Select(new("selected.txt", " M", false, true), false);
            window.UpdateLayout();
            Assert.False(action.IsEffectivelyEnabled);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertWithin(Control control, Control parent)
    {
        var position = control.TranslatePoint(default, parent)!.Value;
        Assert.InRange(position.X, 0, parent.Bounds.Width);
        Assert.True(position.X + control.Bounds.Width <= parent.Bounds.Width + 1);
        Assert.True(position.Y + control.Bounds.Height <= parent.Bounds.Height + 1);
    }

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }

    private static void Save(AppWindow window, bool dark, int width, string state)
    {
        if (Environment.GetEnvironmentVariable("MOSSAGENT_GIT_INDEX_VISUAL_OUTPUT") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, 180), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(output, $"actions-{(dark ? "dark" : "light")}-{width}-{state}.png"), PngBitmapEncoderOptions.Default);
    }
}
