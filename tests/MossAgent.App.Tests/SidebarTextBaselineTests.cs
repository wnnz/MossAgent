using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarTextBaselineTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task TaskTitleUsesMeasuredOpticalBaselineWithoutChangingHitArea(bool dark, bool projectTask) => _session.Dispatch(() =>
    {
        var row = new AppTaskNavigationRow { Title = "排查截图工具黑屏问题", IsProjectTask = projectTask };
        WithWindow(row, dark, window =>
        {
            var button = row.FindControl<AppButton>("SelectTaskButton")!;
            var text = Assert.Single(button.GetVisualDescendants().OfType<AppText>());
            Assert.Equal(new Matrix(1, 0, 0, 1, 0, -1), Assert.IsType<TranslateTransform>(text.RenderTransform).Value);
            Assert.Equal(14, text.FontSize);
            Assert.Equal(18, text.LineHeight);
            Assert.Equal(30, row.Bounds.Height);
            Assert.Equal(30, button.Bounds.Height);
            Assert.Equal(projectTask ? 32 : 8, text.TranslatePoint(default, row)!.Value.X, precision: 3);
            Assert.Equal(5, text.TranslatePoint(default, row)!.Value.Y, precision: 3);
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ProjectTitleKeepsMeasuredFolderGeometryWithoutTextTransform(bool dark, bool expanded) => _session.Dispatch(() =>
    {
        var row = new AppProjectNavigationRow
        {
            Title = "MossAgent",
            IsExpanded = expanded,
            ActionTarget = new object(),
            SelectCommand = new RelayCommand(() => { })
        };
        WithWindow(row, dark, window =>
        {
            var button = row.FindControl<AppActionButton>("SelectProjectButton")!;
            var text = Assert.Single(button.GetVisualDescendants().OfType<AppText>(), control => control.IsVisible);
            var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>(), control => control.IsVisible);
            Assert.Null(text.RenderTransform);
            Assert.Equal(13, text.FontSize);
            Assert.Equal(31, row.Bounds.Height);
            Assert.Equal(32, text.TranslatePoint(default, row)!.Value.X, precision: 3);
            Assert.Equal(new Matrix(1, 0, 0, 1, expanded ? 1.5 : 2.5, -1.5), icon.RenderTransform!.Value);
            Assert.Equal(expanded ? 13 : 11, icon.Size);
        });
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OrdinaryActionAndTextDoNotInheritSidebarBaselineCorrection(bool dark) => _session.Dispatch(() =>
    {
        var ordinary = new AppText { Text = "普通文字" };
        var action = new AppActionButton { Label = "普通操作", IconData = Geometry.Parse("M0,0 L10,10") };
        WithWindow(new StackPanel { Children = { ordinary, action } }, dark, window =>
        {
            Assert.Null(ordinary.RenderTransform);
            Assert.Null(Assert.Single(action.GetVisualDescendants().OfType<AppText>(), control => control.IsVisible).RenderTransform);
        });
    }, TestContext.Current.CancellationToken);

    private static void WithWindow(Control content, bool dark, Action<AppWindow> check)
    {
        var window = new AppWindow
        {
            Width = 264,
            Height = 100,
            Content = new StackPanel { Children = { content } },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); check(window); }
        finally { window.Close(); }
    }
}
