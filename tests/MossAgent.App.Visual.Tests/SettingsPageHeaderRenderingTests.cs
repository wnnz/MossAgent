using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class SettingsPageHeaderRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320, 1)]
    [InlineData(true, 320, 1)]
    [InlineData(false, 480, 1)]
    [InlineData(true, 480, 1)]
    [InlineData(false, 728, 1)]
    [InlineData(true, 728, 1)]
    [InlineData(false, 320, 1.25)]
    [InlineData(true, 320, 1.25)]
    [InlineData(false, 728, 2)]
    [InlineData(true, 728, 2)]
    public Task Header_ReflowsReadableStatusAndActions(bool dark, int width, double scale) => _session.Dispatch(() =>
    {
        var create = new AppActionButton { Label = "添加提供商", Classes = { "primary" }, Command = new RelayCommand(() => { }) };
        var refresh = new AppActionButton { Label = "刷新", Classes = { "ghost" }, Command = new RelayCommand(() => { }) };
        var header = new AppSettingsPageHeader
        {
            Title = "AI 提供商",
            Description = "管理连接、模型列表与模型参数。",
            Status = "暂时无法刷新配置，请检查连接后重试。当前输入内容会保留。",
            IsStatusError = true,
            Actions = new StackPanel { Classes = { "settings-header-actions" }, Children = { refresh, create } },
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var window = new AppWindow
        {
            Width = width,
            Height = 320,
            Content = header,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            Save(window, FormattableString.Invariant($"header-{dark}-{width}-{scale}x"));
            var status = Assert.Single(header.GetVisualDescendants().OfType<AppText>(), text => text.Text == header.Status);
            Assert.True(status.TextLayout.WidthIncludingTrailingWhitespace <= status.Bounds.Width + 1);
            Assert.Equal(Color.Parse(dark ? "#F87171" : "#B42318"), Assert.IsAssignableFrom<ISolidColorBrush>(status.Foreground).Color);
            var actions = header.FindControl<AppContentControl>("ActionsPresenter")!;
            var heading = header.FindControl<StackPanel>("HeadingContent")!;
            Assert.Equal(width < 480 ? 1 : 0, Grid.GetRow(actions));
            if (width < 480) Assert.True(actions.Bounds.Y >= heading.Bounds.Bottom);
            Assert.InRange(create.TranslatePoint(default, header)!.Value.X + create.Bounds.Width, 0, header.Bounds.Width);
            Assert.Equal(create.Bounds.Height, refresh.Bounds.Height);
            header.Status = null;
            header.Description = null;
            header.Actions = null;
            window.UpdateLayout();
            Assert.False(status.IsVisible);
            Assert.False(header.HasStatus);
            Assert.False(header.HasDescription);
            Assert.Equal(0, header.FindControl<Grid>("HeaderLayout")!.RowSpacing);
            Assert.Null(actions.Content);
            var title = Assert.Single(header.GetVisualDescendants().OfType<AppText>(), text => text.Classes.Contains("settings-title"));
            Assert.Equal(title.Bounds.Height, header.Bounds.Height);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Header_LiveResizeAndActionReplacementKeepTheSameContent(bool dark) => _session.Dispatch(() =>
    {
        var first = new AppButton { Content = "刷新" };
        var header = new AppSettingsPageHeader { Title = "网络代理", Actions = first };
        var window = new AppWindow { Width = 728, Height = 200, Content = header, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            var actions = header.FindControl<AppContentControl>("ActionsPresenter")!;
            foreach (var width in new[] { 320, 479, 480, 728, 320, 728 })
            {
                window.Width = width;
                window.UpdateLayout();
                Assert.Equal(width < 480 ? 1 : 0, Grid.GetRow(actions));
                Assert.Same(first, actions.Content);
            }
            var replacement = new AppButton { Content = "取消刷新" };
            header.Actions = replacement;
            header.Status = "正在刷新";
            window.UpdateLayout();
            Assert.Same(replacement, actions.Content);
            Assert.True(header.HasStatus);
            Assert.Null(first.GetVisualParent());
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Save(AppWindow window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
