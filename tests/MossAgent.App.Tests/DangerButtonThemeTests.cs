using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DangerButtonThemeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EnabledButton_UsesItsThemePalette(bool dark) => WithWindow(dark, (window, button) =>
    {
        AssertPalette(button, dark);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.Equal(new Thickness(1), button.BorderThickness);
        Assert.Equal(new CornerRadius(14), button.CornerRadius);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EnabledButton_KeepsReadableTextContrast(bool dark) => WithWindow(dark, (window, button) =>
    {
        var background = Luminance(Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);
        var foreground = Luminance(Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color);
        Assert.True((Math.Max(background, foreground) + 0.05) / (Math.Min(background, foreground) + 0.05) >= 4.5);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ThemeSwitch_UpdatesExistingButtonWithoutReplacingItsCommand(bool dark) => WithWindow(dark, (window, button) =>
    {
        var command = new RelayCommand(() => { });
        button.Command = command;
        window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
        AssertPalette(button, !dark);
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        AssertPalette(button, dark);
        Assert.Same(command, button.Command);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HoverAndPress_RetainDangerPaletteAndHitArea(bool dark) => WithWindow(dark, (window, button) =>
    {
        var hitArea = button.Bounds.Size;
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        Assert.True(button.IsPointerOver);
        AssertPalette(button, dark);
        window.MouseDown(center, MouseButton.Left);
        AssertPalette(button, dark);
        window.MouseUp(center, MouseButton.Left);
        Assert.Equal(hitArea, button.Bounds.Size);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisabledCommand_UsesMutedPaletteAndCannotExecute(bool dark) => WithWindow(dark, (window, button) =>
    {
        var invoked = 0;
        var enabled = false;
        var command = new RelayCommand(() => invoked++, () => enabled);
        button.Command = command;
        Assert.False(button.IsEffectivelyEnabled);
        AssertResource(button, button.Background, "ButtonBackgroundBrush");
        AssertResource(button, button.Foreground, "TextSubtleBrush");
        AssertResource(button, button.BorderBrush, "BorderSubtleBrush");
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        Assert.Equal(0, invoked);
        enabled = true;
        command.NotifyCanExecuteChanged();
        Assert.True(button.IsEffectivelyEnabled);
        AssertPalette(button, dark);
    });

    private Task WithWindow(bool dark, Action<AppWindow, AppButton> test) => _session.Dispatch(() =>
    {
        var button = new AppButton { Content = "删除", Classes = { "danger" } };
        var window = new AppWindow
        {
            Width = 320,
            Height = 200,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { button } }
        };
        window.Show();
        try
        {
            window.MouseMove(new Point(300, 180));
            window.UpdateLayout();
            test(window, button);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertPalette(AppButton button, bool dark)
    {
        AssertBrush(button.Background, dark ? "#2E1517" : "#FEF2F2");
        AssertBrush(button.BorderBrush, dark ? "#6B2127" : "#FECACA");
        AssertBrush(button.Foreground, dark ? "#F87171" : "#B91C1C");
    }

    private static void AssertBrush(IBrush? brush, string color) =>
        Assert.Equal(Color.Parse(color), Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color);

    private static void AssertResource(AppButton button, IBrush? brush, string key)
    {
        Assert.True(button.TryFindResource(key, button.ActualThemeVariant, out var resource));
        Assert.Same(resource, brush);
    }

    private static double Luminance(Color color) =>
        0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);

    private static double Linearize(byte component)
    {
        var normalized = component / 255d;
        return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }
}
