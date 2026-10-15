using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ShutdownOverlayComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(true, 980, 640)]
    [InlineData(false, 800, 600)]
    [InlineData(true, 800, 600)]
    public Task SharedDialogKeepsPendingAndFailureStatesInsideItsSurface(bool dark, int width, int height) => _session.Dispatch(() =>
    {
        var state = new AppShutdownViewModel();
        var overlay = new AppShutdownOverlay { DataContext = state };
        var window = new AppWindow
        {
            Width = width,
            Height = height,
            Content = overlay,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            var dialog = Assert.IsType<AppSettingsDialog>(overlay.Content);
            state.Begin();
            window.UpdateLayout();
            Assert.True(dialog.IsVisible);
            Assert.False(dialog.IsCloseButtonVisible);
            Assert.Null(dialog.CloseCommand);
            var footer = Assert.IsType<StackPanel>(dialog.Footer);
            var buttons = footer.Children.OfType<AppButton>().ToArray();
            var force = Assert.Single(buttons, button => ReferenceEquals(button.Command, state.ForceExitCommand));
            var retry = Assert.Single(buttons, button => ReferenceEquals(button.Command, state.RetryExitCommand));
            Assert.False(footer.IsVisible);
            Assert.False(retry.IsDefault);
            dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Assert.True(state.IsPending);
            state.Fail();
            window.UpdateLayout();
            Assert.True(footer.IsVisible);
            Assert.True(force.IsEffectivelyEnabled);
            Assert.True(retry.IsEffectivelyEnabled);
            Assert.True(retry.IsDefault);
            Assert.False(force.IsDefault);
            var surface = Assert.Single(dialog.GetVisualDescendants().OfType<AppCard>());
            Assert.True(surface.Bounds.Width > 0);
            Assert.True(surface.Bounds.Height > 0);
            Assert.True(surface.Bounds.Width < window.Bounds.Width);
            Assert.True(surface.Bounds.Height < window.Bounds.Height);
            Assert.NotEqual(default, surface.BoxShadow);
            foreach (var button in buttons)
            {
                var origin = button.TranslatePoint(default, surface)!.Value;
                Assert.InRange(origin.X, 0, surface.Bounds.Width);
                Assert.InRange(origin.X + button.Bounds.Width, 0, surface.Bounds.Width);
                Assert.InRange(origin.Y + button.Bounds.Height, 0, surface.Bounds.Height);
            }
            Assert.All(dialog.GetVisualDescendants().OfType<AppIconButton>(), button => Assert.False(button.IsVisible));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
