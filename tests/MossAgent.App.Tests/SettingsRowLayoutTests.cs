using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsRowLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Resize_KeepsLabelsControlsAndTheFocusedDraft(bool dark) => _session.DispatchAsync(() =>
    {
        var label = new AppText { Text = "浏览器的授权资料目录与连接配置", TextWrapping = TextWrapping.Wrap };
        var input = new AppInput { Text = "retained draft" };
        var row = new AppSettingsRow { Children = { label, input } };
        var window = new AppWindow
        {
            Width = 640,
            Height = 300,
            Content = row,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            foreach (var width in new[] { 640, 296, 416, 415, 640 })
            {
                window.Width = width;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(width < 416, row.IsCompact);
                Assert.Equal("retained draft", input.Text);
                Assert.True(label.TextLayout.WidthIncludingTrailingWhitespace <= label.Bounds.Width + 1);
                Assert.True(input.Focus(NavigationMethod.Tab));
                Assert.True(input.IsFocused);
                Assert.True(new Rect(row.Bounds.Size).Contains(input.Bounds));
                if (row.IsCompact)
                {
                    Assert.True(input.Bounds.Top >= label.Bounds.Bottom + 8,
                        $"Width {width}: input {input.Bounds}, label {label.Bounds}, row spacing {row.RowSpacing}");
                    Assert.Equal(row.Bounds.Width, input.Bounds.Width);
                }
                else
                {
                    Assert.True(input.Bounds.Left >= label.Bounds.Right + row.Gap);
                    Assert.Equal(row.ControlWidth, input.Bounds.Width);
                }
            }
            input.SelectAll();
            window.KeyTextInput("edited after resize");
            Assert.Equal("edited after resize", input.Text);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidGeometry_IsRejected(double value)
    {
        var row = new AppSettingsRow();
        Assert.Throws<ArgumentException>(() => row.MinimumLabelWidth = value);
        Assert.Throws<ArgumentException>(() => row.ControlWidth = value);
        if (value != 0) Assert.Throws<ArgumentException>(() => row.Gap = value);
    }
}
