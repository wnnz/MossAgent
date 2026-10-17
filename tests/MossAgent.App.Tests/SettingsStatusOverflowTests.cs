using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsStatusOverflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 486)]
    [InlineData(true, 486)]
    [InlineData(false, 1024)]
    [InlineData(true, 1024)]
    public Task LongDiscoveryErrorPreservesVisibleProviderListAndActions(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new SettingsStatusOverflowFixture(dark, width);
        await fixture.Models.RefreshProvidersCommand.ExecuteAsync(null);
        fixture.Flush();
        var previousViewport = fixture.PageScroll.Viewport.Height;
        Assert.True(previousViewport > 200);
        await fixture.FailDiscoveryAsync();
        fixture.Save($"long-status-{dark}-{width}");
        Assert.True(fixture.PageScroll.Viewport.Height >= previousViewport / 2,
            $"Long feedback shrank the list viewport from {previousViewport} to {fixture.PageScroll.Viewport.Height}.");
        fixture.AssertOnScreen(fixture.Create);
        fixture.Press(fixture.Create);
        Assert.True(fixture.Providers.IsFormOpen);
        fixture.Send(Key.Escape);
        Assert.False(fixture.Providers.IsFormOpen);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 486)]
    [InlineData(true, 486)]
    [InlineData(false, 1024)]
    [InlineData(true, 1024)]
    public Task PendingLongDiscoveryKeepsControlsVisibleAndCompletesNormally(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new SettingsStatusOverflowFixture(dark, width);
        await fixture.Models.RefreshProvidersCommand.ExecuteAsync(null);
        fixture.Flush();
        var previousViewport = fixture.PageScroll.Viewport.Height;
        var operation = fixture.Models.FetchModelsForProviderCommand.ExecuteAsync(Assert.Single(fixture.Models.ProviderGroups));
        await fixture.Discovery.Started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        fixture.Flush();
        try
        {
            Assert.False(fixture.Header.IsStatusError);
            Assert.Contains("正在从", fixture.Header.Status);
            Assert.True(fixture.PageScroll.Viewport.Height >= previousViewport / 2);
            fixture.AssertOnScreen(fixture.Create);
            fixture.Save($"long-status-loading-{dark}-{width}");
        }
        finally
        {
            fixture.Discovery.Complete([]);
            await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Flush();
        }
        Assert.False(fixture.Models.HasStatusError);
        Assert.Equal(fixture.Models.Status, fixture.Header.Status);
        Assert.True(fixture.PageScroll.Viewport.Height >= previousViewport / 2);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 486)]
    [InlineData(true, 486)]
    [InlineData(false, 1024)]
    [InlineData(true, 1024)]
    public Task FullErrorIsKeyboardReadableWithoutMovingTheProviderList(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new SettingsStatusOverflowFixture(dark, width);
        await fixture.Models.RefreshProvidersCommand.ExecuteAsync(null);
        await fixture.FailDiscoveryAsync();
        var viewer = fixture.Header.FindControl<AppScrollViewer>("StatusScroll")!;
        Assert.Equal(fixture.Models.Status, AutomationProperties.GetName(viewer));
        Assert.Equal(fixture.Models.Status, Assert.IsType<AppText>(viewer.Content).Text);
        Assert.True(viewer.Focusable);
        Assert.True(viewer.Extent.Height > viewer.Viewport.Height);
        Assert.InRange(viewer.Bounds.Height, 1, 96);
        var pageOffset = fixture.PageScroll.Offset;
        var refresh = Assert.Single(fixture.Header.GetVisualDescendants().OfType<AppActionButton>(),
            button => ReferenceEquals(button.Command, fixture.Providers.RefreshCommand));
        Assert.True(refresh.Focus(NavigationMethod.Tab));
        fixture.Send(Key.Tab, RawInputModifiers.Shift);
        Assert.Same(viewer, fixture.Window.FocusManager!.GetFocusedElement());
        fixture.Send(Key.PageDown);
        Assert.True(viewer.Offset.Y > 0);
        for (var attempt = 0; attempt < 100 && viewer.Offset.Y + viewer.Viewport.Height < viewer.Extent.Height - 0.5; attempt++)
            fixture.Press(viewer, Key.PageDown);
        Assert.InRange(viewer.Extent.Height - viewer.Viewport.Height - viewer.Offset.Y, 0, 0.5);
        fixture.Save($"long-status-keyboard-{dark}-{width}");
        for (var attempt = 0; attempt < 100 && viewer.Offset.Y > 0; attempt++) fixture.Press(viewer, Key.PageUp);
        Assert.Equal(0, viewer.Offset.Y);
        Assert.Equal(pageOffset, fixture.PageScroll.Offset);
        fixture.Press(fixture.Create);
        fixture.Send(Key.PageDown);
        Assert.Equal(0, viewer.Offset.Y);
        fixture.Send(Key.Escape);
        Assert.False(fixture.Providers.IsFormOpen);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 486)]
    [InlineData(true, 486)]
    [InlineData(false, 1024)]
    [InlineData(true, 1024)]
    public Task ShortAndClearedStatusesKeepCompactGeometryAndOtherScrollViewersUnchanged(bool dark, int width) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsStatusOverflowFixture(dark, width);
        var previousHeight = fixture.Header.Bounds.Height;
        var previousViewport = fixture.PageScroll.Viewport.Height;
        fixture.Header.Status = "连接配置已保存。";
        fixture.Flush();
        var viewer = fixture.Header.FindControl<AppScrollViewer>("StatusScroll")!;
        Assert.True(viewer.IsEffectivelyVisible);
        Assert.InRange(fixture.Header.Bounds.Height - previousHeight, 1, 24);
        Assert.True(viewer.Extent.Height <= viewer.Viewport.Height);
        Assert.True(double.IsPositiveInfinity(fixture.PageScroll.MaxHeight));
        Assert.False(fixture.PageScroll.Focusable);
        fixture.Header.Status = null;
        fixture.Flush();
        Assert.False(viewer.IsEffectivelyVisible);
        Assert.Equal(previousHeight, fixture.Header.Bounds.Height);
        Assert.Equal(previousViewport, fixture.PageScroll.Viewport.Height);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
