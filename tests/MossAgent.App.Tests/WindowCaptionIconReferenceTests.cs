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
public sealed class WindowCaptionIconReferenceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(false, 1280, 820)]
    [InlineData(true, 980, 640)]
    [InlineData(true, 1280, 820)]
    [InlineData(false, 981, 640)]
    [InlineData(false, 1281, 820)]
    [InlineData(true, 981, 640)]
    [InlineData(true, 1281, 820)]
    public Task CaptionIcons_MatchReferenceWithoutChangingHitAreas(bool dark, double width, double height) =>
        _session.DispatchAsync(() =>
        {
            var window = new MainWindow
            {
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                WindowState = WindowState.Normal,
                Width = width,
                Height = height
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var buttons = window.GetVisualDescendants().OfType<AppIconButton>()
                    .Where(button => button.Classes.Contains("window-control")).ToArray();
                Assert.Equal(3, buttons.Length);
                foreach (var button in buttons)
                {
                    var icon = Assert.IsType<AppIcon>(button.Content);
                    var expectedSize = button.Classes.Contains("closeButton") ? 10 : button.Classes.Contains("window-maximize") ? 9 : 8;
                    Assert.Equal(expectedSize, icon.Size);
                    Assert.Equal(1, icon.StrokeThickness);
                    Assert.Equal(button.Classes.Contains("window-maximize") ? PenLineCap.Flat : PenLineCap.Round, icon.StrokeLineCap);
                    Assert.Equal(new Size(46, 36), button.Bounds.Size);
                    Assert.Equal("Arrow", button.Cursor?.ToString());
                    var center = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), button);
                    Assert.NotNull(center);
                    Assert.Equal(23, center.Value.X, 2);
                    Assert.InRange(center.Value.Y, 17.5, 18.5);
                }
                AssertCloseHover(window, Assert.Single(buttons, button => button.Classes.Contains("closeButton")));
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

    private static void AssertCloseHover(MainWindow window, AppIconButton button)
    {
        var position = button.TranslatePoint(new Point(23, 18), window);
        Assert.NotNull(position);
        window.MouseMove(position.Value);
        window.UpdateLayout();
        Assert.True(button.IsPointerOver);
        Assert.Equal(Color.Parse("#E81123"), Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(Assert.IsType<AppIcon>(button.Content).Stroke).Color);
        Assert.Equal("Arrow", button.Cursor?.ToString());
        window.MouseMove(new Point(window.Width / 2, 100));
        window.UpdateLayout();
        Assert.Equal(0, Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color.A);
    }
}
