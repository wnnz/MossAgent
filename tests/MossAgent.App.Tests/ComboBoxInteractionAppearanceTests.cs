using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComboBoxInteractionAppearanceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task KeyboardFocus_UsesNeutralBackgroundAndVisibleBorder(bool dark, bool compact) => _session.Dispatch(() =>
    {
        WithSelector(dark, compact, (window, selector) =>
        {
            Assert.True(selector.Focus(NavigationMethod.Tab));
            window.UpdateLayout();
            Assert.True(selector.IsFocused);
            Assert.True(selector.IsKeyboardFocusWithin);
            Assert.True(Highlight(selector).IsVisible);
            AssertSurface(selector, "InputBackgroundBrush", "BorderFocusBrush");
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardSelectionAndPopup_PreserveNativeBehavior(bool dark) => _session.Dispatch(() =>
    {
        WithSelector(dark, false, (window, selector) =>
        {
            Assert.True(selector.Focus(NavigationMethod.Tab));
            PressKey(window, Key.Down);
            Assert.Equal("high", selector.SelectedItem);
            PressKey(window, Key.F4);
            window.UpdateLayout();
            Assert.True(selector.IsDropDownOpen);
            PressKey(window, Key.Up);
            PressKey(window, Key.Enter);
            window.UpdateLayout();
            Assert.False(selector.IsDropDownOpen);
            Assert.Equal("medium", selector.SelectedItem);
            AssertSurface(selector, "InputBackgroundBrush", "BorderFocusBrush");
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisabledSelector_RejectsFocusAndSelectionUntilEnabled(bool dark) => _session.Dispatch(() =>
    {
        WithSelector(dark, false, (window, selector) =>
        {
            selector.IsEnabled = false;
            window.UpdateLayout();
            Assert.False(selector.Focus(NavigationMethod.Tab));
            PressKey(window, Key.Down);
            Assert.Equal("medium", selector.SelectedItem);
            Assert.False(selector.IsDropDownOpen);
            selector.IsEnabled = true;
            Assert.True(selector.Focus(NavigationMethod.Tab));
            PressKey(window, Key.Down);
            Assert.Equal("high", selector.SelectedItem);
            AssertSurface(selector, "InputBackgroundBrush", "BorderFocusBrush");
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FocusedSelector_FollowsLiveThemeChanges(bool dark) => _session.Dispatch(() =>
    {
        WithSelector(dark, false, (window, selector) =>
        {
            Assert.True(selector.Focus(NavigationMethod.Tab));
            window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
            window.UpdateLayout();
            Assert.Equal(window.ActualThemeVariant, selector.ActualThemeVariant);
            Assert.True(Highlight(selector).IsVisible);
            AssertSurface(selector, "InputBackgroundBrush", "BorderFocusBrush");
            Assert.Equal("medium", selector.SelectedItem);
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task PointerOver_UsesNeutralBackgroundWithOrWithoutFocus(bool dark, bool focused) => _session.Dispatch(() =>
    {
        WithSelector(dark, false, (window, selector) =>
        {
            if (focused) Assert.True(selector.Focus(NavigationMethod.Tab));
            window.MouseMove(selector.TranslatePoint(new Point(20, 16), window)!.Value);
            window.UpdateLayout();
            Assert.True(selector.IsPointerOver);
            AssertSurface(selector, "InputBackgroundBrush", "BorderFocusBrush");
        });
    }, TestContext.Current.CancellationToken);

    private static void AssertSurface(AppComboBox selector, string backgroundKey, string borderKey)
    {
        var background = Assert.Single(selector.GetVisualDescendants().OfType<Border>(), border => border.Name == "Background");
        Assert.True(Avalonia.Application.Current!.TryGetResource(backgroundKey, selector.ActualThemeVariant, out var expectedBackground));
        Assert.True(Avalonia.Application.Current.TryGetResource(borderKey, selector.ActualThemeVariant, out var expectedBorder));
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expectedBackground).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(background.Background).Color);
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expectedBorder).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(background.BorderBrush).Color);
        var highlight = Highlight(selector);
        if (highlight.IsVisible)
        {
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expectedBackground).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(highlight.Background).Color);
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expectedBorder).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(highlight.BorderBrush).Color);
        }
        foreach (var presenter in selector.GetVisualDescendants().OfType<ContentPresenter>())
        {
            if (presenter.Background is ISolidColorBrush brush && brush.Color.A != 0)
                Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expectedBackground).Color, brush.Color);
        }
        if (selector.IsFocused) AssertFocusForeground(selector);
    }

    private static void AssertFocusForeground(AppComboBox selector)
    {
        Assert.True(Avalonia.Application.Current!.TryGetResource("TextPrimaryBrush", selector.ActualThemeVariant, out var primary));
        Assert.True(Avalonia.Application.Current.TryGetResource("TextMutedBrush", selector.ActualThemeVariant, out var muted));
        var content = Assert.Single(selector.GetVisualDescendants().OfType<ContentControl>(), control => control.Name == "ContentPresenter");
        var glyph = Assert.Single(selector.GetVisualDescendants().OfType<PathIcon>(), control => control.Name == "DropDownGlyph");
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(primary).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(content.Foreground).Color);
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(muted).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(glyph.Foreground).Color);
    }

    private static Border Highlight(AppComboBox selector) =>
        Assert.Single(selector.GetVisualDescendants().OfType<Border>(), border => border.Name == "HighlightBackground");

    private static void PressKey(AppWindow window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static void WithSelector(bool dark, bool compact, Action<AppWindow, AppComboBox> inspect)
    {
        var selector = new AppComboBox { ItemsSource = new[] { "low", "medium", "high" }, SelectedIndex = 1 };
        if (compact) selector.Classes.Add("compact-selector");
        var window = new AppWindow
        {
            Width = 400,
            Height = 180,
            Content = new StackPanel { Children = { selector, new AppInput { Text = "Other field" } } },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            inspect(window, selector);
        }
        finally { window.Close(); }
    }
}
