using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Headless;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class ButtonFocusRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "default", 1)]
    [InlineData(true, "default", 1)]
    [InlineData(false, "primary", 1)]
    [InlineData(true, "primary", 1)]
    [InlineData(false, "ghost", 1)]
    [InlineData(true, "ghost", 1)]
    [InlineData(false, "danger", 1)]
    [InlineData(true, "danger", 1)]
    [InlineData(false, "icon", 1)]
    [InlineData(true, "icon", 1)]
    [InlineData(false, "history", 1)]
    [InlineData(true, "history", 1)]
    [InlineData(false, "settings", 1)]
    [InlineData(true, "settings", 1)]
    [InlineData(false, "send", 1)]
    [InlineData(true, "send", 1)]
    [InlineData(false, "rail", 1)]
    [InlineData(true, "rail", 1)]
    [InlineData(false, "conversation", 1)]
    [InlineData(true, "conversation", 1)]
    [InlineData(false, "ghost", 1.25)]
    [InlineData(true, "ghost", 1.25)]
    [InlineData(false, "send", 2)]
    [InlineData(true, "send", 2)]
    public Task KeyboardRing_FollowsButtonShapeWithoutChangingLayout(bool dark, string kind, double scale) => _session.Dispatch(() =>
    {
        using var fixture = new ButtonFocusRenderFixture(dark, kind, scale);
        var button = fixture.Button;
        Assert.True(button.Focus(NavigationMethod.Pointer));
        using var pointer = fixture.Capture("pointer");
        var bounds = button.Bounds;
        var radius = button.CornerRadius;
        var content = button.GetVisualDescendants().First();
        var contentOrigin = content.TranslatePoint(default, fixture.Window);
        Assert.True(fixture.Outside.Focus(NavigationMethod.Pointer));
        Assert.True(button.Focus(NavigationMethod.Tab));
        using var focused = fixture.Capture("keyboard");
        Assert.True(button.IsKeyboardFocusWithin);
        Assert.Equal(fixture.Pixel(pointer, new Point(0.5, 0.5)), fixture.Pixel(focused, new Point(0.5, 0.5)));
        Assert.NotEqual(fixture.Pixel(pointer, new Point(bounds.Width / 2, 0.5)), fixture.Pixel(focused, new Point(bounds.Width / 2, 0.5)));
        var ring = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("button-focus-ring"));
        Assert.Equal(button.CornerRadius, ring.CornerRadius);
        Assert.Equal(new Thickness(2), ring.BorderThickness);
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(fixture.Window.FindResource(fixture.Window.ActualThemeVariant, "BorderFocusBrush")).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color);
        Assert.False(ring.IsHitTestVisible);
        var target = Assert.IsAssignableFrom<Avalonia.Visual>(AdornerLayer.GetAdornedElement(ring));
        Assert.True(ReferenceEquals(button, target) || button.IsVisualAncestorOf(target));
        Assert.Equal(bounds, button.Bounds);
        Assert.Equal(contentOrigin, content.TranslatePoint(default, fixture.Window));
        button.CornerRadius = new CornerRadius(14);
        fixture.Flush();
        Assert.Equal(button.CornerRadius, ring.CornerRadius);
        Assert.True(fixture.Outside.Focus(NavigationMethod.Pointer));
        button.CornerRadius = radius;
        using var blurred = fixture.Capture("blurred");
        Assert.True(pointer.Pixels.SequenceEqual(blurred.Pixels));
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("button-focus-ring"));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitFocusOwner_DoesNotGetAnAdditionalSharedRing(bool dark) => _session.Dispatch(() =>
    {
        using var fixture = new ButtonFocusRenderFixture(dark, "ghost", 1);
        fixture.Button.FocusAdorner = null;
        Assert.True(fixture.Button.Focus(NavigationMethod.Pointer));
        using var pointer = fixture.Capture("owned-pointer");
        Assert.True(fixture.Outside.Focus(NavigationMethod.Pointer));
        Assert.True(fixture.Button.Focus(NavigationMethod.Tab));
        using var focused = fixture.Capture("owned-keyboard");
        Assert.True(pointer.Pixels.SequenceEqual(focused.Pixels));
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("button-focus-ring"));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FocusFeedback_DoesNotConsumeKeyboardOrPointerActions(bool dark) => _session.Dispatch(() =>
    {
        using var fixture = new ButtonFocusRenderFixture(dark, "ghost", 1);
        var executions = 0;
        var enabled = true;
        var command = new RelayCommand(() => executions++, () => enabled);
        fixture.Button.Command = command;
        Assert.True(fixture.Button.Focus(NavigationMethod.Tab));
        fixture.Key(Key.Space, PhysicalKey.Space);
        Assert.Equal(1, executions);
        var point = fixture.Button.TranslatePoint(new Point(fixture.Button.Bounds.Width / 2, fixture.Button.Bounds.Height / 2), fixture.Window)!.Value;
        fixture.Window.MouseMove(point);
        fixture.Flush();
        Assert.True(fixture.Button.IsPointerOver);
        Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("button-focus-ring"));
        fixture.Window.MouseDown(point, MouseButton.Left);
        fixture.Window.MouseUp(point, MouseButton.Left);
        Assert.Equal(2, executions);
        enabled = false;
        command.NotifyCanExecuteChanged();
        fixture.Key(Key.Space, PhysicalKey.Space);
        fixture.Window.MouseDown(point, MouseButton.Left);
        fixture.Window.MouseUp(point, MouseButton.Left);
        Assert.False(fixture.Button.IsEffectivelyEnabled);
        Assert.Equal(2, executions);
    }, TestContext.Current.CancellationToken);
}
