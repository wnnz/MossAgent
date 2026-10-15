using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WindowCaptionContrastTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(false, 1280, 820)]
    [InlineData(true, 980, 640)]
    [InlineData(true, 1280, 820)]
    public Task CaptionPaletteDoesNotInheritMutedGhostIconsOrChangeHoverAndHitAreas(bool dark, int width, int height) =>
        _session.Dispatch(() =>
        {
            var window = new MainWindow
            {
                Width = width,
                Height = height,
                WindowState = WindowState.Normal,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.MouseMove(new Point(width / 2, 100));
                window.UpdateLayout();
                var buttons = window.GetVisualDescendants().OfType<AppIconButton>()
                    .Where(button => button.Classes.Contains("window-control")).ToArray();
                Assert.Equal(3, buttons.Length);
                var expected = Color.Parse(dark ? "#FFFFFF" : "#1F1F1F");
                var muted = Color.Parse(dark ? "#B4B4B4" : "#5D5D5D");
                foreach (var button in buttons)
                {
                    AssertColor(button, expected);
                    Assert.Equal(new Size(46, 36), button.Bounds.Size);
                    Assert.Equal("Arrow", button.Cursor?.ToString());
                    Assert.False(button.Focusable);
                    var position = button.TranslatePoint(new Point(23, 18), window)!.Value;
                    window.MouseMove(position);
                    window.UpdateLayout();
                    Assert.True(button.IsPointerOver);
                    AssertColor(button, button.Classes.Contains("closeButton") ? Colors.White : expected);
                    if (button.Classes.Contains("closeButton"))
                        Assert.Equal(Color.Parse("#E81123"), Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);
                    window.MouseMove(new Point(width / 2, 100));
                    window.UpdateLayout();
                    AssertColor(button, expected);
                    Assert.Equal(0, Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color.A);
                    button.IsEnabled = false;
                    window.UpdateLayout();
                    AssertColor(button, muted);
                    button.IsEnabled = true;
                    window.UpdateLayout();
                    AssertColor(button, expected);
                }
                var navigation = Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
                    button => button.Classes.Contains("titlebar-panel"));
                AssertColor(navigation, muted);
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                window.UpdateLayout();
                foreach (var button in buttons) AssertColor(button, Color.Parse(dark ? "#1F1F1F" : "#FFFFFF"));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void AssertColor(AppIconButton button, Color expected)
    {
        Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color);
        Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(Assert.IsType<AppIcon>(button.Content).Stroke).Color);
    }
}
