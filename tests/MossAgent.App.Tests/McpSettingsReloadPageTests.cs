using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class McpSettingsReloadPageTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980)]
    [InlineData(true, 980)]
    [InlineData(false, 728)]
    [InlineData(true, 728)]
    public Task Reconnect_CanCancelAndRetryFromKeyboard(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new McpSettingsReloadFixture(initialFailure: true, blockReconnect: true);
        fixture.Repository.Reader = null;
        var pane = new McpSettingsPane { DataContext = fixture.Model };
        var window = new AppWindow
        {
            Width = width,
            Height = 700,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            var buttons = pane.GetVisualDescendants().OfType<AppButton>().ToArray();
            var reconnect = Assert.Single(buttons, button => ReferenceEquals(button.Command, fixture.Model.ReloadCommand));
            var cancel = Assert.Single(buttons, button => ReferenceEquals(button.Command, fixture.Model.CancelReloadCommand));
            var create = Assert.Single(buttons, button => ReferenceEquals(button.Command, fixture.Model.OpenCreateFormCommand));
            var list = Assert.Single(pane.GetVisualDescendants().OfType<AppListBox>());
            Assert.False(cancel.IsVisible);
            PressSpace(reconnect);
            var run = fixture.Model.ReloadCommand.ExecutionTask!;
            await fixture.Manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Flush(window);
            Assert.False(reconnect.IsVisible);
            Assert.True(cancel.IsVisible);
            Assert.False(create.IsEffectivelyEnabled);
            Assert.False(list.IsEffectivelyEnabled);
            PressSpace(cancel);
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Flush(window);
            Assert.Contains("已取消", fixture.Model.Status);
            fixture.Manager.Release.TrySetResult();
            PressSpace(reconnect);
            await fixture.Model.ReloadCommand.ExecutionTask!;
            Flush(window);
            Assert.Single(fixture.Model.Items);
            Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsItemRow>());
            Assert.True(list.IsEffectivelyEnabled);
            Assert.True(create.IsEffectivelyEnabled);
            Assert.False(cancel.IsVisible);
            Assert.True(reconnect.IsVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void PressSpace(Control button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
