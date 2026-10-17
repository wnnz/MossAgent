using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class SelectorFocusRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "regular", 1)]
    [InlineData(true, "regular", 1)]
    [InlineData(false, "compact", 1)]
    [InlineData(true, "compact", 1)]
    [InlineData(false, "regular", 1.25)]
    [InlineData(true, "regular", 1.25)]
    [InlineData(false, "compact", 1.25)]
    [InlineData(true, "compact", 1.25)]
    [InlineData(false, "regular", 2)]
    [InlineData(true, "regular", 2)]
    [InlineData(false, "compact", 2)]
    [InlineData(true, "compact", 2)]
    public Task KeyboardItemRing_IsRoundedWithoutMovingContent(bool dark, string kind, double scale) => _session.Dispatch(() =>
    {
        using var fixture = new SelectorFocusRenderFixture(dark, kind, scale);
        var item = fixture.OpenAndNavigate();
        Assert.True(item.IsFocused);
        Assert.Equal("medium", fixture.Selector.SelectedItem);
        var other = Assert.IsType<ComboBoxItem>(fixture.Selector.ContainerFromIndex(0));
        Assert.True(other.Focus(NavigationMethod.Pointer));
        Assert.True(item.Focus(NavigationMethod.Pointer));
        using var pointer = fixture.Capture("pointer");
        var bounds = item.Bounds;
        var selectorBounds = fixture.Selector.Bounds;
        var content = item.GetVisualDescendants().OfType<TextBlock>().Single();
        var origin = content.TranslatePoint(default, fixture.Window);
        Assert.True(other.Focus(NavigationMethod.Pointer));
        Assert.True(item.Focus(NavigationMethod.Tab));
        using var focused = fixture.Capture("keyboard");
        Assert.Equal(fixture.Pixel(pointer, item, new Point(0.5, 0.5)), fixture.Pixel(focused, item, new Point(0.5, 0.5)));
        Assert.NotEqual(fixture.Pixel(pointer, item, new Point(bounds.Width / 2, 0.5)),
            fixture.Pixel(focused, item, new Point(bounds.Width / 2, 0.5)));
        var ring = AssertRing(fixture, item);
        Assert.Equal(bounds, item.Bounds);
        Assert.Equal(selectorBounds, fixture.Selector.Bounds);
        Assert.Equal(origin, content.TranslatePoint(default, fixture.Window));
        item.CornerRadius = new CornerRadius(10);
        fixture.Flush();
        Assert.Equal(item.CornerRadius, ring.CornerRadius);
        fixture.Key(Key.Escape, PhysicalKey.Escape);
        Assert.False(fixture.Selector.IsDropDownOpen);
        Assert.Equal("medium", fixture.Selector.SelectedItem);
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), HasRingClass);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, "regular")]
    [InlineData(true, "regular")]
    [InlineData(false, "compact")]
    [InlineData(true, "compact")]
    public Task Ring_FollowsThemeWithoutBlockingSelectionOrDisabledBehavior(bool dark, string kind) => _session.Dispatch(() =>
    {
        using var fixture = new SelectorFocusRenderFixture(dark, kind, 1);
        var changes = 0;
        fixture.Selector.SelectionChanged += (_, _) => changes++;
        var item = fixture.OpenAndNavigate();
        var ring = AssertRing(fixture, item);
        fixture.Window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
        fixture.Flush();
        Assert.Same(ring, AssertRing(fixture, item));
        fixture.Key(Key.Enter, PhysicalKey.Enter);
        Assert.Equal("high", fixture.Selector.SelectedItem);
        Assert.Equal(1, changes);
        Assert.False(fixture.Selector.IsDropDownOpen);
        fixture.Key(Key.F4, PhysicalKey.F4);
        var target = Assert.IsType<ComboBoxItem>(fixture.Selector.ContainerFromIndex(1));
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), fixture.Window)!.Value;
        var hit = Assert.IsAssignableFrom<Avalonia.Visual>(fixture.Window.InputHitTest(point));
        Assert.True(ReferenceEquals(target, hit) || target.IsVisualAncestorOf(hit));
        fixture.Window.MouseMove(point);
        fixture.Window.MouseDown(point, MouseButton.Left);
        fixture.Window.MouseUp(point, MouseButton.Left);
        fixture.Flush();
        Assert.Equal("medium", fixture.Selector.SelectedItem);
        Assert.Equal(2, changes);
        Assert.False(fixture.Selector.IsDropDownOpen);
        fixture.Selector.IsEnabled = false;
        Assert.False(fixture.Selector.Focus(NavigationMethod.Tab));
        fixture.Key(Key.F4, PhysicalKey.F4);
        fixture.Key(Key.Down, PhysicalKey.ArrowDown);
        Assert.False(fixture.Selector.IsDropDownOpen);
        Assert.Equal("medium", fixture.Selector.SelectedItem);
        Assert.Equal(2, changes);
        fixture.Selector.IsEnabled = true;
        Assert.True(fixture.Selector.Focus(NavigationMethod.Tab));
        fixture.Key(Key.Down, PhysicalKey.ArrowDown);
        Assert.Equal("high", fixture.Selector.SelectedItem);
        Assert.Equal(3, changes);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1.25)]
    [InlineData(true, 1.25)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public Task PillHighlight_RetainsItsOwnRoundedFocusFeedback(bool dark, double scale) => _session.Dispatch(() =>
    {
        using var fixture = new SelectorFocusRenderFixture(dark, "pill", scale);
        var item = fixture.OpenAndNavigate();
        var other = Assert.IsType<ComboBoxItem>(fixture.Selector.ContainerFromIndex(0));
        Assert.Null(item.FocusAdorner);
        Assert.True(other.Focus(NavigationMethod.Pointer));
        Assert.True(item.Focus(NavigationMethod.Pointer));
        using var pointer = fixture.Capture("owned-pointer");
        Assert.True(other.Focus(NavigationMethod.Pointer));
        Assert.True(item.Focus(NavigationMethod.Tab));
        using var focused = fixture.Capture("owned-keyboard");
        Assert.Equal(fixture.Pixel(pointer, item, new Point(0.5, 0.5)), fixture.Pixel(focused, item, new Point(0.5, 0.5)));
        Assert.NotEqual(fixture.Pixel(pointer, item, new Point(item.Bounds.Width / 2, 0.5)),
            fixture.Pixel(focused, item, new Point(item.Bounds.Width / 2, 0.5)));
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), HasRingClass);
        fixture.Key(Key.Enter, PhysicalKey.Enter);
        Assert.Equal("high", fixture.Selector.SelectedItem);
        Assert.False(fixture.Selector.IsDropDownOpen);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitFocusOwner_CanOptOutOfTheSharedAdorner(bool dark) => _session.Dispatch(() =>
    {
        using var fixture = new SelectorFocusRenderFixture(dark, "regular", 1);
        var item = fixture.OpenAndNavigate();
        item.FocusAdorner = null;
        var other = Assert.IsType<ComboBoxItem>(fixture.Selector.ContainerFromIndex(0));
        Assert.True(other.Focus(NavigationMethod.Pointer));
        Assert.True(item.Focus(NavigationMethod.Pointer));
        using var pointer = fixture.Capture("explicit-pointer");
        Assert.True(other.Focus(NavigationMethod.Pointer));
        Assert.True(item.Focus(NavigationMethod.Tab));
        using var focused = fixture.Capture("explicit-keyboard");
        Assert.True(pointer.Pixels.SequenceEqual(focused.Pixels));
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), HasRingClass);
    }, TestContext.Current.CancellationToken);

    private static AppBorder AssertRing(SelectorFocusRenderFixture fixture, ComboBoxItem item)
    {
        var ring = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), HasRingClass);
        Assert.Equal(item.CornerRadius, ring.CornerRadius);
        Assert.Equal(new Thickness(2), ring.BorderThickness);
        Assert.False(ring.IsHitTestVisible);
        var target = Assert.IsAssignableFrom<Avalonia.Visual>(AdornerLayer.GetAdornedElement(ring));
        Assert.True(ReferenceEquals(item, target) || item.IsVisualAncestorOf(target));
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(fixture.Window.FindResource(fixture.Window.ActualThemeVariant, "BorderFocusBrush")).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color);
        return ring;
    }

    private static bool HasRingClass(AppBorder border) => border.Classes.Contains("selector-focus-ring");
}
