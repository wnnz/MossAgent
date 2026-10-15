using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CheckBoxHitTargetTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task GlyphLabelAndBlankAreas_RespectTheSameEnabledHitTarget(bool dark, bool disabled) => _session.Dispatch(() =>
    {
        var checkbox = new AppCheckBox
        {
            Content = "共享当前对话上下文",
            IsChecked = false,
            IsEnabled = !disabled,
            VerticalAlignment = VerticalAlignment.Top
        };
        var window = new AppWindow
        {
            Width = 340,
            Height = 100,
            Content = checkbox,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var points = new[] { new Point(8, 14), new Point(65, 14), new Point(300, 14), new Point(65, 2), new Point(300, 26) };
            foreach (var local in points)
            {
                checkbox.IsChecked = false;
                var point = checkbox.TranslatePoint(local, window)!.Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.Equal(!disabled, checkbox.IsChecked);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task BlankArea_RemainsInsideTheNativeThreeStateCycle() => _session.Dispatch(() =>
    {
        var checkbox = new AppCheckBox
        {
            Content = "Three states",
            IsThreeState = true,
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Top
        };
        var window = new AppWindow { Width = 340, Height = 100, Content = checkbox };
        window.Show();
        try
        {
            window.UpdateLayout();
            var point = checkbox.TranslatePoint(new Point(300, 14), window)!.Value;
            bool?[] expected = [true, null, false];
            foreach (var selected in expected)
            {
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.Equal(selected, checkbox.IsChecked);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
