using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemePreviewRenderedTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "light")]
    [InlineData(true, "light")]
    [InlineData(false, "dark")]
    [InlineData(true, "dark")]
    public Task StandardSceneKeepsMeasuredConversationGeometry(bool dark, string mode) =>
        _session.Dispatch(() =>
        {
            Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var preview = CreatePreview(mode);
            var window = Show(preview);
            try
            {
                Assert.Equal(new Size(80, 60), preview.Bounds.Size);
                var scene = preview.GetVisualDescendants().OfType<AppThemePreviewScene>()
                    .Single(control => control.Classes.Contains("preview-standard-scene"));
                var background = Part(scene, "preview-backdrop").Background;
                if (mode == "dark") Assert.Equal(Color.Parse("#262626"), Brush(background));
                else
                {
                    Assert.Equal(Color.Parse("#F4F4F4"), Brush(background));
                }
                var screen = Part(scene, "preview-screen");
                Assert.Equal(new Size(66, 46), screen.Bounds.Size);
                Assert.Equal(new Point(7, 7), screen.TranslatePoint(default, scene));
                Assert.Equal(Color.Parse(mode == "dark" ? "#0D0D0D" : "#FFFFFF"), Brush(screen.Background));
                var rows = scene.GetVisualDescendants().OfType<AppBorder>()
                    .Where(control => control.Classes.Contains("preview-message-line")).ToArray();
                Assert.Equal(3, rows.Length);
                Assert.Equal([30d, 18d, 36d], rows.Select(row => row.Bounds.Width));
                Assert.All(rows, row => Assert.Equal(2, row.Bounds.Height));
                Assert.All(rows, row => Assert.Equal(Color.Parse(mode == "dark" ? "#565656" : "#E5E5E5"), Brush(row.Background)));
                Assert.Equal(3, scene.GetVisualDescendants().OfType<AppBorder>()
                    .Count(control => control.Classes.Contains("preview-accent-line")));
                Assert.Equal(8, Part(scene, "preview-composer").Bounds.Height);
                AssertPart(scene, "preview-user-first", new Point(38, 12), new Size(30, 2));
                AssertPart(scene, "preview-assistant-first", new Point(24, 18), new Size(30, 2));
                AssertPart(scene, "preview-assistant-second", new Point(24, 22), new Size(18, 2));
                AssertPart(scene, "preview-assistant-third", new Point(24, 26), new Size(36, 2));
                AssertPart(scene, "preview-user-second", new Point(29, 32), new Size(39, 2));
                AssertPart(scene, "preview-user-third", new Point(45, 36), new Size(23, 2));
                AssertPart(scene, "preview-composer", new Point(24, 41), new Size(44, 8));
                AssertPart(scene, "preview-send", new Point(63, 43), new Size(4, 4));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SystemSceneClipsTwoRealConversationScenesAtTheCenter(bool dark) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var preview = CreatePreview("system");
        var window = Show(preview);
        try
        {
            var scenes = preview.GetVisualDescendants().OfType<AppThemePreviewScene>()
                .Where(control => !control.Classes.Contains("preview-standard-scene")).ToArray();
            Assert.Equal(2, scenes.Length);
            Assert.All(scenes, scene => Assert.Equal(new Size(80, 60), scene.Bounds.Size));
            Assert.Equal(new Point(0, 0), scenes[0].TranslatePoint(default, preview));
            Assert.Equal(new Point(40, 0), scenes[1].TranslatePoint(default, preview));
            Assert.All(scenes, scene =>
            {
                var clip = Assert.IsType<AppBorder>(scene.GetVisualParent());
                Assert.True(clip.ClipToBounds);
                Assert.Equal(new Size(40, 60), clip.Bounds.Size);
            });
            Assert.Equal(Color.Parse("#FFFFFF"), Brush(Part(scenes[0], "preview-screen").Background));
            Assert.Equal(Color.Parse("#0D0D0D"), Brush(Part(scenes[1], "preview-screen").Background));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, "system")]
    [InlineData(false, "light")]
    [InlineData(false, "dark")]
    [InlineData(true, "system")]
    [InlineData(true, "light")]
    [InlineData(true, "dark")]
    public Task SelectedSceneKeepsSharedOutlineAndVisibility(bool dark, string mode) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var preview = CreatePreview(mode);
        preview.Classes.Add("selected");
        var window = Show(preview);
        try
        {
            var outline = Part(preview, "preview-selection-outline");
            Assert.Equal(new Thickness(2), outline.BorderThickness);
            Assert.Equal(new CornerRadius(10), outline.CornerRadius);
            Assert.Equal(Color.Parse(dark ? "#EEEEEE" : "#202020"), Brush(outline.BorderBrush));
            var scenes = preview.GetVisualDescendants().OfType<Grid>().ToArray();
            Assert.Equal(mode == "system", scenes.Single(control => control.Classes.Contains("preview-system-scene")).IsVisible);
            Assert.Equal(mode != "system", preview.GetVisualDescendants().OfType<AppThemePreviewScene>()
                .Single(control => control.Classes.Contains("preview-standard-scene")).IsVisible);
            Assert.False(preview.IsHitTestVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppThemePreview CreatePreview(string mode)
    {
        var preview = new AppThemePreview();
        preview.Classes.Add("preview-" + mode);
        return preview;
    }

    private static AppWindow Show(Control preview)
    {
        var window = new AppWindow { Width = 248, Height = 174, Content = preview };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static AppBorder Part(Control preview, string name) => preview.GetVisualDescendants()
        .OfType<AppBorder>().Single(control => control.Classes.Contains(name));

    private static Color Brush(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static void AssertPart(Control scene, string name, Point origin, Size size)
    {
        var part = Part(scene, name);
        Assert.Equal(origin, part.TranslatePoint(default, scene));
        Assert.Equal(size, part.Bounds.Size);
    }
}
