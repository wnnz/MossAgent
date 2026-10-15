using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsDialogScrollTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, 640, false)]
    [InlineData(980, 640, true)]
    [InlineData(1280, 820, false)]
    [InlineData(1280, 820, true)]
    [InlineData(1920, 1032, false)]
    [InlineData(1920, 1032, true)]
    public Task ReopeningStartsAtTheFirstLabelWithoutChangingTheDraft(int width, int height, bool dark) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture(dark);
        fixture.Window.Width = width;
        fixture.Window.Height = height;
        var (label, _) = AddScrollableContent(fixture);
        fixture.Open();
        var scroll = BodyScroll(fixture);
        Assert.Equal(0, scroll.Offset.Y);
        ScrollToEnd(scroll, fixture);
        fixture.Dialog.IsOpen = false;
        fixture.Flush();

        fixture.Open();

        Assert.True(fixture.First.IsFocused);
        Assert.Equal("模型 ID 草稿", fixture.First.Text);
        Assert.Equal(0, scroll.Offset.Y);
        AssertVisible(label, scroll);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReturningToAnOpenPageRetainsItsScrollAndRememberedField(bool dark) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture(dark);
        var (_, last) = AddScrollableContent(fixture);
        fixture.Open();
        var scroll = BodyScroll(fixture);
        last.Focus();
        fixture.Flush();
        ScrollToEnd(scroll, fixture);
        var offset = scroll.Offset;
        fixture.HidePage();

        fixture.ShowPage();

        Assert.True(last.IsFocused);
        Assert.Equal(offset, scroll.Offset);
        AssertVisible(last, scroll);
        Assert.True(fixture.Dialog.IsOpen);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitFocusAfterOpeningCanStillScrollToALaterField(bool dark) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture(dark);
        var (_, last) = AddScrollableContent(fixture);
        fixture.Open();
        fixture.Dialog.IsOpen = false;
        fixture.Flush();

        fixture.Dialog.IsOpen = true;
        fixture.Window.UpdateLayout();
        last.Focus();
        fixture.Flush();

        var scroll = BodyScroll(fixture);
        Assert.True(last.IsFocused);
        Assert.True(scroll.Offset.Y > 0);
        AssertVisible(last, scroll);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReopeningResetsOnlyTheDialogBodyNotNestedContentScrollers() => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        AddScrollableContent(fixture);
        var nestedContent = new StackPanel();
        for (var index = 0; index < 32; index++) nestedContent.Children.Add(new AppInput { Text = $"nested {index}" });
        var nested = new AppScrollViewer { Height = 120, Content = nestedContent };
        Assert.IsType<StackPanel>(fixture.Dialog.Content).Children.Add(nested);
        fixture.Open();
        var scroll = BodyScroll(fixture);
        nested.Offset = new Vector(0, 80);
        ScrollToEnd(scroll, fixture);
        Assert.Equal(80, nested.Offset.Y);
        fixture.Dialog.IsOpen = false;
        fixture.Flush();

        fixture.Open();

        Assert.Equal(0, scroll.Offset.Y);
        Assert.Equal(80, nested.Offset.Y);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    private static (AppText Label, AppInput Last) AddScrollableContent(SettingsDialogFocusFixture fixture)
    {
        Assert.IsType<StackPanel>(fixture.Dialog.Content).Children.Clear();
        var label = new AppText { Text = "模型 ID" };
        var content = new StackPanel { Children = { label, fixture.First, fixture.Second } };
        AppInput last = fixture.Second;
        for (var index = 0; index < 32; index++)
        {
            last = new AppInput { Text = $"synthetic field {index}" };
            content.Children.Add(last);
        }
        fixture.Dialog.Content = content;
        return (label, last);
    }

    private static AppScrollViewer BodyScroll(SettingsDialogFocusFixture fixture) => fixture.Dialog.GetVisualDescendants()
        .OfType<AppScrollViewer>().Single(scroll => scroll.IsVisualAncestorOf(fixture.First));

    private static void ScrollToEnd(AppScrollViewer scroll, SettingsDialogFocusFixture fixture)
    {
        scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
        fixture.Flush();
        Assert.True(scroll.Offset.Y > 0);
    }

    private static void AssertVisible(Control control, AppScrollViewer scroll)
    {
        var position = control.TranslatePoint(default, scroll);
        Assert.NotNull(position);
        Assert.True(position.Value.Y >= 0);
        Assert.True(position.Value.Y + control.Bounds.Height <= scroll.Viewport.Height + 1);
    }
}
