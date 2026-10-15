using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsSharedAppearanceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, "#ECEFF1")]
    [InlineData(false, true, "#ECEFF1")]
    [InlineData(true, false, "#2C333A")]
    [InlineData(true, true, "#2C333A")]
    public Task SettingsNavigation_UsesSharedMetricsAndThemeSelection(bool dark, bool selected, string selection) =>
        _session.Dispatch(() =>
        {
            Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            Assert.True(Avalonia.Application.Current.TryGetResource("UiIconAppearance",
                dark ? ThemeVariant.Dark : ThemeVariant.Light, out var appearanceIcon));
            var navigation = new AppSettingsNavButton
            {
                Label = "外观",
                IsChecked = selected,
                IconData = Assert.IsAssignableFrom<Geometry>(appearanceIcon)
            };
            var search = new AppInput { PlaceholderText = "搜索" };
            search.Classes.Add("settings-search");
            var window = new AppWindow
            {
                Width = 276,
                Height = 200,
                Content = new StackPanel { Children = { navigation, search } }
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var content = Assert.IsType<AppIconLabel>(navigation.Content);
                var icon = Assert.IsType<AppIcon>(content.Children[0]);
                var label = Assert.IsType<AppText>(content.Children[1]);
                Assert.Equal(14, icon.Size);
                Assert.Equal(1, icon.StrokeThickness);
                Assert.Equal(10, content.Spacing);
                Assert.Equal(new Thickness(0, 0, 10, 0), icon.Margin);
                Assert.Equal(new Thickness(0), navigation.Margin);
                Assert.Equal(14, navigation.FontSize);
                Assert.Equal(14, label.FontSize);
                Assert.Equal(18, label.LineHeight);
                Assert.Equal(FontWeight.Normal, label.FontWeight);
                Assert.Equal(VerticalAlignment.Center, icon.VerticalAlignment);
                Assert.Equal(VerticalAlignment.Center, label.VerticalAlignment);
                Assert.InRange(Math.Abs(icon.Bounds.Center.Y - label.Bounds.Center.Y), 0, 0.01);
                Assert.Equal(Color.Parse(selection), Assert.IsType<SolidColorBrush>(search.Background).Color);
                if (selected)
                    Assert.Equal(Color.Parse(selection), Assert.IsType<SolidColorBrush>(navigation.Background).Color);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SettingsHeading_UsesSharedMeasuredTypography(bool dark) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var heading = new AppText { Text = "外观" };
        heading.Classes.Add("settings-title");
        var window = new AppWindow { Width = 800, Height = 100, Content = heading };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(28, heading.FontSize);
            Assert.Equal(FontWeight.Normal, heading.FontWeight);
            Assert.Equal(Color.Parse(dark ? "#ECECEC" : "#1A1C1F"),
                Assert.IsType<SolidColorBrush>(heading.Foreground).Color);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);
}
