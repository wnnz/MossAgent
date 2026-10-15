using Avalonia.Automation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemePaletteChoiceInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PopupKeyboardFocusUsesTheSharedHighlightInsteadOfABlackFrame(bool dark) =>
        _session.Dispatch(() =>
        {
            using var fixture = new ThemePaletteChoiceFixture(dark);
            var accent = Selector(fixture, "主题强调色");
            Assert.True(accent.Focus(NavigationMethod.Tab));
            Press(fixture, Key.F4);
            Press(fixture, Key.Down);
            var item = Assert.IsType<ComboBoxItem>(accent.ContainerFromIndex(1));
            Assert.True(item.IsKeyboardFocusWithin);
            Assert.True(item.IsSet(Control.FocusAdornerProperty));
            Assert.Null(item.FocusAdorner);
            var content = Assert.Single(item.GetVisualDescendants().OfType<ContentPresenter>(),
                presenter => presenter.Name == "PART_ContentPresenter");
            Assert.True(Avalonia.Application.Current!.TryGetResource("ButtonHoverBrush", accent.ActualThemeVariant,
                out var expected));
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expected).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(content.Background).Color);
            Press(fixture, Key.Escape);
            Assert.False(accent.IsDropDownOpen);
            Assert.Null(fixture.Store.LoadThemePalette(fixture.Theme));
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SwatchHitAreaOpensTheNativePopupAndKeyboardFocusIsVisible(bool dark) =>
        _session.Dispatch(() =>
        {
            using var fixture = new ThemePaletteChoiceFixture(dark);
            var accent = Selector(fixture, "主题强调色");
            var swatch = accent.GetVisualDescendants().OfType<AppColorSwatch>().Single();
            var point = swatch.TranslatePoint(new Point(7, 7), fixture.Window)!.Value;
            fixture.Window.MouseDown(point, MouseButton.Left);
            fixture.Window.MouseUp(point, MouseButton.Left);
            fixture.Flush();
            Assert.True(accent.IsDropDownOpen);
            Press(fixture, Key.Escape);
            Assert.False(accent.IsDropDownOpen);
            Assert.True(Selector(fixture, "主题界面字体").Focus(NavigationMethod.Tab));
            Assert.True(accent.Focus(NavigationMethod.Tab));
            fixture.Flush();
            var highlight = Assert.Single(accent.GetVisualDescendants().OfType<AppBorder>(),
                border => border.Name == "HighlightBackground");
            Assert.True(highlight.IsVisible);
            Assert.True(Avalonia.Application.Current!.TryGetResource("BorderFocusBrush", accent.ActualThemeVariant,
                out var expected));
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expected).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(highlight.BorderBrush).Color);
            accent.IsEnabled = false;
            fixture.Window.MouseDown(point, MouseButton.Left);
            fixture.Window.MouseUp(point, MouseButton.Left);
            fixture.Flush();
            Assert.False(accent.IsDropDownOpen);
            Assert.Null(fixture.Store.LoadThemePalette(fixture.Theme));
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeKeyboardPopupAndDisabledBehaviorPersistOnlyTheSelectedTheme(bool dark) =>
        _session.Dispatch(() =>
        {
            using var fixture = new ThemePaletteChoiceFixture(dark);
            var accent = Selector(fixture, "主题强调色");
            var font = Selector(fixture, "主题界面字体");
            Assert.True(accent.Focus(NavigationMethod.Tab));
            Press(fixture, Key.Down);
            Assert.Equal("#2563EB", fixture.Store.LoadThemePalette(fixture.Theme)?.AccentHex);
            Assert.Equal("#2563EB", accent.GetVisualDescendants().OfType<AppColorSwatch>().Single().ColorHex);
            Press(fixture, Key.F4);
            Assert.True(accent.IsDropDownOpen);
            Press(fixture, Key.Escape);
            Assert.False(accent.IsDropDownOpen);
            Assert.Equal("#2563EB", fixture.Editor.SelectedAccentOption?.Value);
            Assert.True(font.Focus(NavigationMethod.Tab));
            Press(fixture, Key.Down);
            Assert.Equal(UiFontPreference.SegoeUi, fixture.Store.LoadUiFont(fixture.Theme));
            Assert.True(font.Bounds.Width > 60);
            Assert.Contains(font.GetVisualDescendants().OfType<AppText>(), text => text.Text == "Segoe UI");
            Press(fixture, Key.F4);
            Assert.True(font.IsDropDownOpen);
            Press(fixture, Key.Down);
            Press(fixture, Key.Enter);
            Assert.False(font.IsDropDownOpen);
            Assert.Equal(UiFontPreference.Inter, fixture.Store.LoadUiFont(fixture.Theme));
            accent.IsEnabled = false;
            Assert.False(accent.Focus(NavigationMethod.Tab));
            Press(fixture, Key.Down);
            Assert.Equal("#2563EB", fixture.Store.LoadThemePalette(fixture.Theme)?.AccentHex);
            accent.IsEnabled = true;
            Assert.True(accent.Focus(NavigationMethod.Tab));
            Press(fixture, Key.Up);
            Assert.Null(fixture.Store.LoadThemePalette(fixture.Theme)?.AccentHex);
            var other = dark ? ThemePreference.Light : ThemePreference.Dark;
            Assert.Null(fixture.Store.LoadThemePalette(other));
            Assert.Null(fixture.Store.LoadUiFont(other));
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task BoundSaveFailureKeepsMalformedAndUnrelatedPreferences(bool dark, bool fontChoice) =>
        _session.Dispatch(() =>
        {
            using var fixture = new ThemePaletteChoiceFixture(dark);
            var original = fontChoice ? "{\"ThemeUiFonts\":\"opaque\",\"Other\":42}"
                : "{\"ThemePalettes\":\"opaque\",\"Other\":42}";
            var path = Path.Combine(fixture.Root, "ui-settings.json");
            File.WriteAllText(path, original);
            var selector = Selector(fixture, fontChoice ? "主题界面字体" : "主题强调色");
            Assert.True(selector.Focus(NavigationMethod.Tab));
            Press(fixture, Key.Down);
            Assert.Equal(original, File.ReadAllText(path));
            Assert.True(fontChoice ? fixture.Editor.Fonts.HasError : fixture.Editor.HasError);
            Assert.False(fontChoice ? fixture.Editor.Fonts.HasStatus : fixture.Editor.HasStatus);
            Assert.Contains(fixture.Card.GetVisualDescendants().OfType<AppText>(),
                text => text.Classes.Contains("status-error") && text.IsVisible && !string.IsNullOrWhiteSpace(text.Text));
        }, TestContext.Current.CancellationToken);

    private static AppComboBox Selector(ThemePaletteChoiceFixture fixture, string name) =>
        Assert.Single(fixture.Card.GetVisualDescendants().OfType<AppComboBox>(),
            selector => AutomationProperties.GetName(selector) == name);

    private static void Press(ThemePaletteChoiceFixture fixture, Key key)
    {
        fixture.Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Flush();
    }
}
