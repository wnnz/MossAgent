using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppearanceHeadingTypographyTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "Segoe UI", 403)]
    [InlineData(true, "Segoe UI", 403)]
    [InlineData(false, "Segoe UI", 728)]
    [InlineData(true, "Segoe UI", 728)]
    [InlineData(false, "fonts:Inter#Inter", 403)]
    [InlineData(true, "fonts:Inter#Inter", 403)]
    [InlineData(false, "fonts:Inter#Inter", 728)]
    [InlineData(true, "fonts:Inter#Inter", 728)]
    public Task SharedSectionTypographyKeepsModeAndPaletteSpacingAcrossFonts(bool dark, string family, double width) => _session.Dispatch(() =>
    {
        var application = Avalonia.Application.Current!;
        var original = application.Resources["UiFontFamily"];
        application.Resources["UiFontFamily"] = new FontFamily(family);
        var pane = new AppearanceSettingsPane();
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
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var title = Assert.Single(pane.GetVisualDescendants().OfType<AppText>(), text => text.Classes.Contains("settings-title"));
            var heading = Assert.Single(pane.GetVisualDescendants().OfType<AppText>(), text => text.Classes.Contains("settings-theme-heading"));
            Assert.Equal(new FontFamily(family), title.FontFamily);
            Assert.Equal(new FontFamily(family), heading.FontFamily);
            Assert.Equal(28, title.FontSize);
            Assert.Equal(14, heading.FontSize);
            Assert.Equal(20, heading.LineHeight);
            Assert.Equal(new Rect(0, title.Bounds.Bottom + 41, width, 20), heading.Bounds);
            var mode = Assert.Single(pane.GetVisualDescendants().OfType<AppThemeModePicker>());
            var palette = Assert.Single(pane.GetVisualDescendants().OfType<AppThemePaletteCard>());
            Assert.Equal(new Rect(0, title.Bounds.Bottom + 77, width, 78), mode.Bounds);
            Assert.Equal(mode.Bounds.Bottom + 16, palette.Bounds.Top);
            if (family == "Segoe UI") Assert.Equal(115, mode.Bounds.Top);
            Assert.Equal(226, palette.GetVisualDescendants().OfType<AppCard>().Single().Bounds.Height);
        }
        finally
        {
            window.Close();
            application.Resources["UiFontFamily"] = original;
            Dispatcher.UIThread.RunJobs();
        }
    }, TestContext.Current.CancellationToken);
}
