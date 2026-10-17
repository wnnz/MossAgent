using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SharedPagePolishTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task EmptyStates_UseOneAlignedWrappingVariant(bool dark, bool catalog) => _session.DispatchAsync(() =>
    {
        var empty = new AppEmptyState
        {
            Title = "尚未配置项目中的连接",
            Description = "添加配置后可以为任务选择使用；较长的说明也必须完整换行，不得截断或覆盖操作入口。"
        };
        if (catalog) empty.Classes.Add("settings-empty");
        var window = new AppWindow
        {
            Content = empty,
            Width = 296,
            Height = 320,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var texts = empty.GetVisualDescendants().OfType<AppText>().ToArray();
            Assert.Equal(2, texts.Length);
            foreach (var text in texts)
            {
                Assert.Equal(catalog ? TextAlignment.Left : TextAlignment.Center, text.TextAlignment);
                Assert.True(text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 1);
            }
            empty.Description = empty.Title;
            window.UpdateLayout();
            Assert.False(empty.HasDescription);
            Assert.False(texts[1].IsVisible);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ListsAndFeedback_UseSharedGeometryAndReadableOverflow(bool dark) => _session.DispatchAsync(() =>
    {
        var list = new AppListBox { ItemsSource = new[] { "Item one", "Item two" }, SelectedIndex = 0 };
        var feedback = new AppText { Text = "一段足够长的错误提示，必须完整显示并换行，避免窄窗口中的反馈被截断。" };
        feedback.Classes.Add("status-error");
        var scroll = new AppScrollViewer { Content = new AppBorder { Height = 600 } };
        scroll.Classes.Add("settings-page-scroll");
        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { list, feedback, scroll } };
        Grid.SetRow(feedback, 1);
        Grid.SetRow(scroll, 2);
        var window = new AppWindow
        {
            Content = content,
            Width = 296,
            Height = 360,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.All(list.GetVisualDescendants().OfType<ListBoxItem>(), item => Assert.Equal(new CornerRadius(8), item.CornerRadius));
            Assert.Equal(TextWrapping.Wrap, feedback.TextWrapping);
            Assert.True(feedback.TextLayout.WidthIncludingTrailingWhitespace <= feedback.Bounds.Width + 1);
            Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
