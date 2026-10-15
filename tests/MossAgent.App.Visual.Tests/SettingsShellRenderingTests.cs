using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class SettingsShellRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640, 1)]
    [InlineData(false, 1280, 820, 1)]
    [InlineData(false, 1920, 1032, 1)]
    [InlineData(false, 980, 640, 1.25)]
    [InlineData(false, 1280, 820, 2)]
    [InlineData(true, 980, 640, 1)]
    [InlineData(true, 1280, 820, 1)]
    [InlineData(true, 1920, 1032, 1)]
    [InlineData(true, 980, 640, 1.25)]
    [InlineData(true, 1280, 820, 2)]
    public Task SettingsShell_RendersSharedPaletteUnclippedShadowAndRoundedCorners(
        bool dark, int width, int height, double scale) => _session.Dispatch(() =>
    {
        var settings = new SettingsOverlay { DataContext = AppearanceContext() };
        var shell = new Grid { RowDefinitions = new RowDefinitions("44,*") };
        shell.Children.Add(new AppBorder { Classes = { "navigation-rail-surface" } });
        Grid.SetRow(settings, 1);
        shell.Children.Add(settings);
        var window = new AppWindow
        {
            Width = width,
            Height = height,
            Content = shell,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            Assert.Equal(scale, window.RenderScaling);
            Assert.Equal(new Size(width, height), window.ClientSize);
            using var bitmap = window.CaptureRenderedFrame();
            Assert.NotNull(bitmap);
            Assert.Equal(new PixelSize((int)(width * scale), (int)(height * scale)), bitmap.PixelSize);
            SaveEvidence(bitmap, dark, width, height, scale);
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            using var pixels = SKBitmap.Decode(stream.ToArray());
            var sidebar = SKColor.Parse(dark ? "#1A2229" : "#F9FBFD");
            var selection = SKColor.Parse(dark ? "#2C333A" : "#ECEFF1");
            Assert.Equal(sidebar, Sample(pixels, 200, 400, scale));
            Assert.Equal(selection, Sample(pixels, 220, 110, scale));
            Assert.Equal(selection, Sample(pixels, 200, 193, scale));
            Assert.Equal(SKColor.Parse(dark ? "#181818" : "#FFFFFF"), Sample(pixels, 324, 400, scale));
            var rail = Sample(pixels, 4, 300, scale);
            var shadow = Sample(pixels, 50, 300, scale);
            Assert.True(shadow.Red < rail.Red && shadow.Green < rail.Green && shadow.Blue < rail.Blue);
            Assert.True(Sample(pixels, 400, 43, scale).Red < Sample(pixels, 400, 20, scale).Red);
            Assert.NotEqual(sidebar, Sample(pixels, 52, 44, scale));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1.25)]
    [InlineData(true, 1.25)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public Task CompactSettingsShowPrimaryAndTemporaryNavigationInBothThemes(bool dark, double scale) => _session.Dispatch(() =>
    {
        var settings = new SettingsOverlay { DataContext = AppearanceContext() };
        var window = new AppWindow
        {
            Width = 526,
            Height = 601,
            Content = settings,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(settings.Navigation.IsCompact);
            Assert.False(settings.Navigation.IsNavigationVisible);
            Assert.Equal(settings.Navigation.Bounds.Width, settings.Navigation.PrimaryPane!.Bounds.Width);
            using var primaryBitmap = window.CaptureRenderedFrame();
            Assert.NotNull(primaryBitmap);
            using var primaryStream = new MemoryStream();
            primaryBitmap.Save(primaryStream, PngBitmapEncoderOptions.Default);
            using var primaryPixels = SKBitmap.Decode(primaryStream.ToArray());
            var main = Sample(primaryPixels, 70, 550, scale);
            Assert.Equal(SKColor.Parse(dark ? "#181818" : "#FFFFFF"), main);
            settings.Navigation.ToggleNavigationCommand.Execute(null);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(settings.Navigation.IsOverlayOpen);
            using var drawerBitmap = window.CaptureRenderedFrame();
            Assert.NotNull(drawerBitmap);
            using var drawerStream = new MemoryStream();
            drawerBitmap.Save(drawerStream, PngBitmapEncoderOptions.Default);
            using var drawerPixels = SKBitmap.Decode(drawerStream.ToArray());
            Assert.Equal(SKColor.Parse(dark ? "#1A2229" : "#F9FBFD"), Sample(drawerPixels, 200, 400, scale));
            var outside = Sample(drawerPixels, 510, 550, scale);
            Assert.True(outside.Red < main.Red && outside.Green < main.Green && outside.Blue < main.Blue);
            settings.Navigation.CloseOverlayCommand.Execute(null);
            Assert.True(settings.Navigation.IsNavigationOpen);
            Assert.False(settings.Navigation.IsOverlayOpen);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static object AppearanceContext() => new
    {
        IsAppearanceSettingsSelected = true,
        IsProviderSettingsSelected = false,
        IsProxySettingsSelected = false,
        IsBrowserSettingsSelected = false,
        IsMcpSettingsSelected = false,
        IsAgentSettingsSelected = false,
        IsSubagentSettingsSelected = false,
        IsAppearanceSettingsMatch = true,
        IsProviderSettingsMatch = true,
        IsProxySettingsMatch = true,
        IsMcpSettingsMatch = true,
        IsBrowserSettingsMatch = true,
        IsAgentSettingsMatch = true,
        IsSubagentSettingsMatch = true,
        HasSettingsSearchResults = true,
        IsPersonalSettingsMatch = true,
        IsIntegrationSettingsMatch = true,
        IsAgentSectionSettingsMatch = true,
        IsSystemThemeSelected = true,
        CloseSettingsCommand = new RelayCommand(() => { }),
        OpenAutomationsCommand = new RelayCommand(() => { }),
        SelectInspectorTabCommand = new RelayCommand<string>(_ => { }),
        ToggleRightPanelCommand = new RelayCommand(() => { })
    };

    private static SKColor Sample(SKBitmap pixels, int column, int row, double scale) =>
        pixels.GetPixel((int)(column * scale), (int)(row * scale));

    private static void SaveEvidence(Bitmap bitmap, bool dark, int width, int height, double scale)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var name = FormattableString.Invariant($"settings-shell-{(dark ? "dark" : "light")}-{width}x{height}-{scale}x.png");
        bitmap.Save(Path.Combine(directory, name), PngBitmapEncoderOptions.Default);
    }
}
