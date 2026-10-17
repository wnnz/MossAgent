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
public sealed class CheckBoxFocusRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, null, 1)]
    [InlineData(true, null, 1)]
    [InlineData(false, false, 1.25)]
    [InlineData(true, false, 1.25)]
    [InlineData(false, true, 1.25)]
    [InlineData(true, true, 1.25)]
    [InlineData(false, null, 1.25)]
    [InlineData(true, null, 1.25)]
    [InlineData(false, false, 2)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 2)]
    [InlineData(false, null, 2)]
    [InlineData(true, null, 2)]
    public Task KeyboardRing_IsRoundedWithoutChangingCheckOrLabelGeometry(bool dark, bool? selected, double scale) => _session.Dispatch(() =>
    {
        using var fixture = new CheckBoxFocusRenderFixture(dark, selected, scale);
        Assert.True(fixture.CheckBox.Focus(NavigationMethod.Pointer));
        using var pointer = fixture.Capture("pointer");
        var parts = fixture.CheckBox.GetVisualDescendants().Where(part => part.Name is "PART_CheckSurface" or "PART_CheckGlyph" or "PART_ContentPresenter").ToArray();
        var origins = parts.Select(part => part.TranslatePoint(default, fixture.Window)).ToArray();
        var bounds = fixture.CheckBox.Bounds;
        Assert.True(fixture.Outside.Focus(NavigationMethod.Pointer));
        Assert.True(fixture.CheckBox.Focus(NavigationMethod.Tab));
        using var focused = fixture.Capture("keyboard");
        Assert.Equal(fixture.Pixel(pointer, new Point(0.5, 0.5)), fixture.Pixel(focused, new Point(0.5, 0.5)));
        Assert.NotEqual(fixture.Pixel(pointer, new Point(bounds.Width / 2, 0.5)), fixture.Pixel(focused, new Point(bounds.Width / 2, 0.5)));
        var ring = Ring(fixture);
        Assert.Equal(new CornerRadius(4), ring.CornerRadius);
        Assert.Equal(new Thickness(2), ring.BorderThickness);
        Assert.False(ring.IsHitTestVisible);
        Assert.Same(fixture.CheckBox, AdornerLayer.GetAdornedElement(ring));
        Assert.Equal(bounds, fixture.CheckBox.Bounds);
        Assert.Equal(origins, parts.Select(part => part.TranslatePoint(default, fixture.Window)).ToArray());
        Assert.Equal(selected, fixture.CheckBox.IsChecked);
        fixture.CheckBox.CornerRadius = new CornerRadius(6);
        fixture.Flush();
        Assert.Equal(new CornerRadius(6), ring.CornerRadius);
        Assert.True(fixture.Outside.Focus(NavigationMethod.Pointer));
        using var blurred = fixture.Capture("blurred");
        Assert.True(pointer.Pixels.SequenceEqual(blurred.Pixels));
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), HasRingClass);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardRing_TracksThemeDuringHoverAndRetainsNativeInteractions(bool dark) => _session.Dispatch(() =>
    {
        using var fixture = new CheckBoxFocusRenderFixture(dark, false, 1);
        Assert.True(fixture.CheckBox.Focus(NavigationMethod.Tab));
        fixture.Space();
        Assert.True(fixture.CheckBox.IsChecked);
        fixture.Space();
        Assert.Null(fixture.CheckBox.IsChecked);
        fixture.Space();
        Assert.False(fixture.CheckBox.IsChecked);
        var point = fixture.CheckBox.TranslatePoint(new Point(80, fixture.CheckBox.Bounds.Height / 2), fixture.Window)!.Value;
        fixture.Window.MouseMove(point);
        fixture.Flush();
        Assert.True(fixture.CheckBox.IsPointerOver);
        var ring = Ring(fixture);
        fixture.Window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
        using var frame = fixture.Capture("theme-hover");
        var expected = Assert.IsAssignableFrom<ISolidColorBrush>(fixture.Window.FindResource(fixture.Window.ActualThemeVariant, "BorderFocusBrush")).Color;
        Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color);
        fixture.Window.MouseDown(point, MouseButton.Left);
        fixture.Window.MouseUp(point, MouseButton.Left);
        Assert.True(fixture.CheckBox.IsChecked);
        fixture.CheckBox.IsEnabled = false;
        fixture.Space();
        fixture.Window.MouseDown(point, MouseButton.Left);
        fixture.Window.MouseUp(point, MouseButton.Left);
        Assert.True(fixture.CheckBox.IsChecked);
        Assert.False(fixture.CheckBox.IsEffectivelyEnabled);
        fixture.CheckBox.IsEnabled = true;
        Assert.True(fixture.CheckBox.Focus(NavigationMethod.Tab));
        fixture.Space();
        Assert.Null(fixture.CheckBox.IsChecked);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitFocusOwner_DoesNotGetAnotherSharedAdorner(bool dark) => _session.Dispatch(() =>
    {
        using var fixture = new CheckBoxFocusRenderFixture(dark, true, 1);
        fixture.CheckBox.FocusAdorner = null;
        Assert.True(fixture.CheckBox.Focus(NavigationMethod.Tab));
        using var frame = fixture.Capture("owned-focus");
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), HasRingClass);
    }, TestContext.Current.CancellationToken);

    private static bool HasRingClass(AppBorder border) => border.Classes.Contains("check-box-focus-ring");
    private static AppBorder Ring(CheckBoxFocusRenderFixture fixture) =>
        Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), HasRingClass);
}
