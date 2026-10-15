using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsShellLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980)]
    [InlineData(true, 980)]
    [InlineData(false, 1280)]
    [InlineData(true, 1280)]
    [InlineData(false, 1920)]
    [InlineData(true, 1920)]
    public Task SettingsShell_UsesReferenceRailSidebarSearchAndOuterFrame(bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            fixture.Main.SelectSettingsTabCommand.Execute("5");
            var window = new MainWindow
            {
                DataContext = fixture.Main,
                WindowState = Avalonia.Controls.WindowState.Normal,
                Width = width,
                Height = 1032,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var settings = Assert.Single(window.GetVisualDescendants().OfType<SettingsOverlay>());
                var rail = Assert.Single(settings.GetVisualDescendants().OfType<AppNavigationRail>());
                Assert.Equal(new Point(0, 44), rail.TranslatePoint(default, window));
                Assert.Equal(52, rail.Bounds.Width);
                var sidebar = Surface(settings, "settings-sidebar-surface");
                Assert.Equal(new Point(52, 44), sidebar.TranslatePoint(default, window));
                Assert.Equal(264, sidebar.Bounds.Width);
                Assert.Equal(new Thickness(0, 0, 1, 0), sidebar.BorderThickness);
                Assert.Equal(Color.Parse(dark ? "#1A2229" : "#F9FBFD"), BrushColor(sidebar.Background));
                var title = Assert.Single(settings.GetVisualDescendants().OfType<AppText>(),
                    control => control.Classes.Contains("settings-sidebar-title"));
                Assert.Equal(new Point(68, 54), title.TranslatePoint(default, window));
                Assert.Equal(18, title.FontSize);
                Assert.Equal(FontWeight.Bold, title.FontWeight);
                Assert.Equal(28, title.LineHeight);
                Assert.Equal(28, title.Bounds.Height);
                var frame = Surface(settings, "window-content-frame");
                var clip = Surface(settings, "window-content-clip");
                Assert.Equal(new CornerRadius(12), frame.CornerRadius);
                Assert.Equal(frame.CornerRadius, clip.CornerRadius);
                Assert.False(frame.ClipToBounds);
                Assert.True(clip.ClipToBounds);
                Assert.False(settings.ClipToBounds);
                Assert.Equal(new Size(width - 56, 984), frame.Bounds.Size);
                var page = Surface(settings, "settings-page-surface");
                Assert.Equal(new Point(316, 44), page.TranslatePoint(default, window));
                Assert.Equal(default, page.CornerRadius);
                var search = Assert.Single(settings.GetVisualDescendants().OfType<AppInput>(),
                    control => control.Classes.Contains("settings-search"));
                Assert.Equal(new Point(60, 96), search.TranslatePoint(default, window));
                Assert.Equal(new Size(247, 36), search.Bounds.Size);
                Assert.Equal(new CornerRadius(18), search.CornerRadius);
                Assert.Equal(Color.Parse(dark ? "#2C333A" : "#ECEFF1"), BrushColor(search.Background));
                var first = Assert.Single(settings.GetVisualDescendants().OfType<AppSettingsNavButton>(),
                    control => control.Label == "外观");
                Assert.Equal(new Point(60, 178), first.TranslatePoint(default, window));
                Assert.Equal(new Size(247, 30), first.Bounds.Size);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static AppBorder Surface(SettingsOverlay settings, string variant) =>
        Assert.Single(settings.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains(variant));

    private static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
