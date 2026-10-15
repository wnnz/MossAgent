using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WindowCaptionFocusTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "window-minimize", true)]
    [InlineData(false, "window-maximize", true)]
    [InlineData(false, "closeButton", true)]
    [InlineData(true, "window-minimize", true)]
    [InlineData(true, "window-maximize", true)]
    [InlineData(true, "closeButton", true)]
    [InlineData(false, "window-minimize", false)]
    [InlineData(false, "window-maximize", false)]
    [InlineData(false, "closeButton", false)]
    [InlineData(true, "window-minimize", false)]
    [InlineData(true, "window-maximize", false)]
    [InlineData(true, "closeButton", false)]
    public Task CaptionClickPreservesDialogFocusAndEscape(bool dark, string role, bool enabled) =>
        _session.DispatchAsync(() =>
        {
            using var fixture = new SettingsDialogFocusFixture(dark);
            var invoked = 0;
            var caption = AddCaption(fixture, role);
            caption.Command = new RelayCommand(() => invoked++, () => enabled);
            fixture.Open();
            fixture.Second.Focus();
            fixture.Second.SelectionStart = 1;
            fixture.Second.SelectionEnd = 3;
            var center = caption.TranslatePoint(new Point(23, 18), fixture.Window)!.Value;

            fixture.Window.MouseDown(center, MouseButton.Left);
            fixture.Window.MouseUp(center, MouseButton.Left);
            fixture.Flush();

            Assert.Equal(enabled ? 1 : 0, invoked);
            Assert.True(fixture.Second.IsFocused);
            Assert.Equal(1, fixture.Second.SelectionStart);
            Assert.Equal(3, fixture.Second.SelectionEnd);
            Assert.Equal("显示名称草稿", fixture.Second.Text);
            Assert.True(fixture.Dialog.IsOpen);
            fixture.PressKey(Key.Escape);
            Assert.False(fixture.Dialog.IsOpen);
            Assert.True(fixture.Navigation.IsFocused);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(true, 980, 640)]
    [InlineData(false, 1280, 820)]
    [InlineData(true, 1280, 820)]
    public Task ProductionCaptionButtonsDoNotBecomeKeyboardStops(bool dark, double width, double height) =>
        _session.DispatchAsync(() =>
        {
            var window = new MainWindow
            {
                Width = width,
                Height = height,
                WindowState = WindowState.Normal,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var buttons = window.GetVisualDescendants().OfType<AppIconButton>()
                    .Where(button => button.Classes.Contains("window-control")).ToArray();
                Assert.Equal(3, buttons.Length);
                Assert.All(buttons, button =>
                {
                    Assert.False(button.Focusable);
                    Assert.False(button.Focus(NavigationMethod.Tab));
                    Assert.True(button.IsHitTestVisible);
                    Assert.False(string.IsNullOrWhiteSpace(button.Label));
                });
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

    private static AppIconButton AddCaption(SettingsDialogFocusFixture fixture, string role)
    {
        var page = Assert.IsAssignableFrom<Control>(fixture.Window.Content);
        fixture.Window.Content = null;
        Grid.SetRow(page, 1);
        var caption = new AppIconButton { Label = role, Classes = { "window-control", role } };
        fixture.Window.Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { caption, page } };
        fixture.Flush();
        return caption;
    }
}
