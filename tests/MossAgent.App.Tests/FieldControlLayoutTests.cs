using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FieldControlLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 640, false)]
    [InlineData(true, 640, false)]
    [InlineData(false, 980, true)]
    [InlineData(true, 980, true)]
    public Task Fields_ShareHeightAndContentAlignment(bool dark, int width, bool disabled) => _session.Dispatch(() =>
    {
        var input = new AppInput { Text = "输入", IsReadOnly = disabled, IsEnabled = !disabled };
        var selector = new AppComboBox { ItemsSource = new[] { "选择" }, SelectedIndex = 0, IsEnabled = !disabled };
        var number = new AppNumericUpDown { Value = 42, IsEnabled = !disabled };
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(selector, 1);
        Grid.SetColumn(number, 2);
        row.Children.Add(input);
        row.Children.Add(selector);
        row.Children.Add(number);
        var window = new AppWindow
        {
            Width = width,
            Height = 200,
            Content = row,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            foreach (var field in new Control[] { input, selector, number })
            {
                Assert.Equal(32, field.Bounds.Height);
                Assert.Equal(32, field.MinHeight);
                Assert.Equal(input.Bounds.Y, field.Bounds.Y);
            }
            Assert.Equal(VerticalAlignment.Center, input.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, selector.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, number.VerticalContentAlignment);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CompactBrowserFields_KeepTheirSharedHeight(bool dark) => _session.Dispatch(() =>
    {
        var input = new AppInput { Text = "localhost", Classes = { "browser-address" } };
        var selector = new AppComboBox
        {
            ItemsSource = new[] { "页面" },
            SelectedIndex = 0,
            Classes = { "compact-selector", "browser-tabs" }
        };
        var window = new AppWindow
        {
            Width = 640,
            Height = 200,
            Content = new StackPanel { Children = { input, selector } },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(28, input.MinHeight);
            Assert.Equal(28, input.Bounds.Height);
            Assert.Equal(28, selector.MinHeight);
            Assert.Equal(28, selector.Bounds.Height);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MultilineAndComposerVariants_PreserveGrowthAndScrolling(bool dark) => _session.Dispatch(() =>
    {
        var multiline = new AppInput { Text = "配置\n第二行", AcceptsReturn = true, Classes = { "field-multiline" } };
        var prompt = new AppInput { Text = "系统提示词", AcceptsReturn = true, Classes = { "field-prompt" } };
        var composer = new AppInput
        {
            Text = string.Join('\n', Enumerable.Repeat("对话内容", 50)),
            AcceptsReturn = true,
            Classes = { "composer-input" }
        };
        var column = new StackPanel { Children = { multiline, prompt, composer }, Spacing = 8 };
        var window = new AppWindow
        {
            Width = 640,
            Height = 500,
            Content = column,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(72, multiline.MinHeight);
            Assert.Equal(150, prompt.MinHeight);
            Assert.Equal(VerticalAlignment.Top, multiline.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Top, prompt.VerticalContentAlignment);
            Assert.True(double.IsNaN(multiline.Height));
            Assert.True(double.IsNaN(prompt.Height));
            Assert.Equal(180, composer.MaxHeight);
            Assert.InRange(composer.Bounds.Height, 36, 180);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
