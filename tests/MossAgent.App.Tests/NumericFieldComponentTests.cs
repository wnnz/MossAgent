using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NumericFieldComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public Task Spinner_UsesSharedSmallCenteredIcons(bool dark, bool left, bool disabled) => _session.Dispatch(() =>
    {
        var field = CreateField(left, disabled);
        WithField(field, dark, window =>
        {
            var spinner = Assert.Single(field.GetVisualDescendants().OfType<ButtonSpinner>());
            var buttons = SpinButtons(spinner);
            Assert.Equal(field.IncreaseLabel, AutomationProperties.GetName(buttons[0]));
            Assert.Equal(field.DecreaseLabel, AutomationProperties.GetName(buttons[1]));
            var text = Assert.Single(field.GetVisualDescendants().OfType<TextBox>());
            Assert.Equal(32, field.Bounds.Height);
            foreach (var button in buttons)
            {
                Assert.Equal(22, button.Bounds.Width);
                Assert.Equal(30, button.Bounds.Height);
                Assert.Equal(new Thickness(0), button.BorderThickness);
                var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
                Assert.Equal(new Size(10, 10), icon.Bounds.Size);
                Assert.False(icon.IsHitTestVisible);
                var center = icon.TranslatePoint(new Point(5, 5), button);
                Assert.NotNull(center);
                Assert.Equal(button.Bounds.Width / 2, center.Value.X, 2);
                Assert.Equal(button.Bounds.Height / 2, center.Value.Y, 2);
                Assert.Equal(disabled, !button.IsEffectivelyEnabled);
            }
            var textPosition = text.TranslatePoint(default, spinner)!.Value.X;
            var actionPosition = buttons[0].TranslatePoint(default, spinner)!.Value.X;
            Assert.Equal(left, actionPosition < textPosition);
            Assert.InRange(text.Bounds.Width, 90, 96);
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task SpinActions_PreserveBoundsAndNativeButtonBehavior(bool dark, bool left) => _session.Dispatch(() =>
    {
        var field = CreateField(left, false);
        field.Minimum = 0;
        field.Maximum = 2;
        field.Value = 1;
        WithField(field, dark, window =>
        {
            var spinner = Assert.Single(field.GetVisualDescendants().OfType<ButtonSpinner>());
            var buttons = SpinButtons(spinner);
            Assert.All(buttons, button => Assert.IsAssignableFrom<RepeatButton>(button));
            buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(2, field.Value);
            Assert.False(buttons[0].IsEnabled);
            Assert.True(buttons[1].IsEnabled);
            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(0, field.Value);
            Assert.False(buttons[1].IsEnabled);
            Assert.True(buttons[0].IsEnabled);
            field.AllowSpin = false;
            Assert.All(buttons, button => Assert.False(button.IsEnabled));
            field.AllowSpin = true;
            field.IsReadOnly = true;
            Assert.All(buttons, button => Assert.False(button.IsEnabled));
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HiddenSpinner_LeavesEditableContentAndLiveThemeIntact(bool dark) => _session.Dispatch(() =>
    {
        var field = CreateField(false, false);
        WithField(field, dark, window =>
        {
            var text = Assert.Single(field.GetVisualDescendants().OfType<TextBox>());
            var before = text.Bounds.Width;
            field.ShowButtonSpinner = false;
            window.UpdateLayout();
            Assert.Equal(before + 44, text.Bounds.Width);
            field.Value = 81;
            Assert.Equal("81", text.Text);
            field.ShowButtonSpinner = true;
            window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
            window.UpdateLayout();
            Assert.Equal(before, text.Bounds.Width);
            Assert.All(field.GetVisualDescendants().OfType<AppIcon>(), icon =>
                Assert.Equal(new Size(10, 10), icon.Bounds.Size));
            Assert.Equal(81, field.Value);
        });
    }, TestContext.Current.CancellationToken);

    private static AppNumericUpDown CreateField(bool left, bool disabled) => new()
    {
        Value = 42,
        Width = 140,
        IsEnabled = !disabled,
        ButtonSpinnerLocation = left ? Location.Left : Location.Right
    };

    private static Button[] SpinButtons(ButtonSpinner spinner) =>
        [Assert.Single(spinner.GetVisualDescendants().OfType<Button>(), button => button.Name == "PART_IncreaseButton"),
         Assert.Single(spinner.GetVisualDescendants().OfType<Button>(), button => button.Name == "PART_DecreaseButton")];

    private static void WithField(AppNumericUpDown field, bool dark, Action<AppWindow> inspect)
    {
        var window = new AppWindow
        {
            Width = 400,
            Height = 200,
            Content = new StackPanel { Children = { field } },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            inspect(window);
        }
        finally { window.Close(); }
    }
}
