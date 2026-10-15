using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemeModeThumbnailGeometryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 384)]
    [InlineData(true, 384)]
    [InlineData(false, 728)]
    [InlineData(true, 728)]
    public Task ThumbnailFillsItsEntireHitAreaWithoutViewboxScaling(bool dark, double width) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var picker = new AppThemeModePicker();
        var window = Show(picker, width);
        try
        {
            var buttons = picker.GetVisualDescendants().OfType<AppButton>().ToArray();
            Assert.Equal(3, buttons.Length);
            foreach (var button in buttons)
            {
                var preview = Assert.Single(button.GetVisualDescendants().OfType<AppThemePreview>());
                Assert.Equal(new Size(80, 60), button.Bounds.Size);
                Assert.Equal(button.Bounds.Size, preview.Bounds.Size);
                Assert.Equal(new Point(0, 0), preview.TranslatePoint(default, button));
                Assert.Equal(default, button.Padding);
                Assert.Equal(default, button.BorderThickness);
                Assert.Null(button.FocusAdorner);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SelectionIsAnOverlayAndDoesNotResizeTheScene(bool dark) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var picker = new AppThemeModePicker();
        var window = Show(picker, 384);
        try
        {
            var button = picker.GetVisualDescendants().OfType<AppButton>().First();
            var preview = Assert.Single(button.GetVisualDescendants().OfType<AppThemePreview>());
            var original = preview.Bounds;
            button.Classes.Add("selected");
            window.UpdateLayout();
            Assert.Equal(original, preview.Bounds);
            var outline = Part(preview, "preview-selection-outline");
            Assert.Equal(new Size(80, 60), outline.Bounds.Size);
            Assert.Equal(new Thickness(2), outline.BorderThickness);
            Assert.Equal(Color.Parse("#399CFF"), ColorOf(outline.BorderBrush));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SystemSceneHasASquareSeamInsideOneRoundedFrame(bool dark) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var preview = new AppThemePreview();
        preview.Classes.Add("preview-system");
        var window = Show(preview, 160);
        try
        {
            var frame = Part(preview, "preview-frame");
            Assert.True(frame.ClipToBounds);
            Assert.Equal(new CornerRadius(10), frame.CornerRadius);
            var scenes = preview.GetVisualDescendants().OfType<AppThemePreviewScene>()
                .Where(scene => !scene.Classes.Contains("preview-standard-scene")).ToArray();
            Assert.Equal(2, scenes.Length);
            Assert.Equal(new Point(40, 0), scenes[1].TranslatePoint(default, preview));
            Assert.All(scenes, scene =>
            {
                Assert.Equal(new Size(80, 60), scene.Bounds.Size);
                Assert.Equal(default, Part(scene, "preview-backdrop").CornerRadius);
                var clip = Assert.IsType<AppBorder>(scene.GetVisualParent());
                Assert.True(clip.ClipToBounds);
                Assert.Equal(new Size(40, 60), clip.Bounds.Size);
            });
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, "System", "#399CFF")]
    [InlineData(true, "System", "#399CFF")]
    [InlineData(false, "Light", "#202020")]
    [InlineData(true, "Light", "#202020")]
    [InlineData(false, "Dark", "#EEEEEE")]
    [InlineData(true, "Dark", "#EEEEEE")]
    public Task KeyboardFocusContrastsWithTheThumbnailWithoutTheNativeFallbackFrame(
        bool dark, string mode, string color) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.SelectThemeCommand.Execute(mode == "Light" ? "Dark" : "Light");
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var picker = new AppThemeModePicker { DataContext = fixture.Main };
        var window = Show(picker, 384);
        try
        {
            var buttons = picker.GetVisualDescendants().OfType<AppButton>().ToArray();
            var button = Assert.Single(buttons, choice => Equals(choice.CommandParameter, mode));
            Assert.True(buttons.First(choice => !ReferenceEquals(choice, button)).Focus());
            Assert.True(button.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            Assert.True(button.IsFocused);
            var outline = Part(button, "preview-selection-outline");
            Assert.Equal(Color.Parse(color), ColorOf(outline.BorderBrush));
            Assert.Null(button.FocusAdorner);
            Assert.DoesNotContain("selected", button.Classes);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisabledModeCannotChangeSavedSelectionThroughKeyboard() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.SelectThemeCommand.Execute("Light");
        var picker = new AppThemeModePicker { DataContext = fixture.Main };
        var window = Show(picker, 384);
        try
        {
            var button = Assert.Single(picker.GetVisualDescendants().OfType<AppButton>(),
                choice => Equals(choice.CommandParameter, "Dark"));
            button.IsEnabled = false;
            Assert.False(button.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(fixture.Main.IsLightThemeSelected);
            Assert.False(fixture.Main.IsDarkThemeSelected);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow Show(Control content, double width)
    {
        var window = new AppWindow { Width = width, Height = 160, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static AppBorder Part(Control control, string name) => control.GetVisualDescendants()
        .OfType<AppBorder>().Single(part => part.Classes.Contains(name));

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
