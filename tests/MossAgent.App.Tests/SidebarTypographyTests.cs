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
public sealed class SidebarTypographyTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SidebarAction_UsesMeasuredTextAndOpticalIconAlignment(bool dark) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var action = new AppActionButton
        {
            Label = "新对话",
            IconData = Geometry.Parse("M2,2 L14,14")
        };
        action.Classes.Add("ghost");
        action.Classes.Add("sidebar-action-button");
        action.Classes.Add("align-leading");
        var window = new AppWindow { Width = 264, Height = 100, Content = action };
        window.Show();
        try
        {
            window.UpdateLayout();
            var label = Assert.Single(action.GetVisualDescendants().OfType<AppText>(), item => item.IsVisible);
            var icon = Assert.Single(action.GetVisualDescendants().OfType<AppIcon>(), item => item.IsVisible);
            Assert.Equal(14, action.FontSize);
            Assert.Equal(14, label.FontSize);
            Assert.Equal(18, label.LineHeight);
            Assert.Equal(11, icon.Size);
            Assert.Equal(1, icon.StrokeThickness);
            Assert.Equal(new Matrix(1, 0, 0, 1, 0.5, -1), icon.RenderTransform!.Value);
            Assert.Equal(label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), action)!.Value.Y - 1,
                icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), action)!.Value.Y, precision: 6);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, "项目")]
    [InlineData(false, "置顶")]
    [InlineData(false, "最近")]
    [InlineData(true, "项目")]
    [InlineData(true, "置顶")]
    [InlineData(true, "最近")]
    public Task SidebarSection_UsesSharedTypographyAndMeasuredTaskSize(bool dark, string title) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var section = new AppText { Text = title };
        section.Classes.Add("sidebar-section-heading");
        var task = new AppText { Text = "会话标题" };
        task.Classes.Add("sidebar-row");
        var window = new AppWindow
        {
            Width = 264,
            Height = 100,
            Content = new StackPanel { Children = { section, task } }
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(14, section.FontSize);
            Assert.Equal(18, section.LineHeight);
            Assert.Equal(FontWeight.Normal, section.FontWeight);
            Assert.Equal(Color.Parse(dark ? "#8E8E8E" : "#8B8D8F"),
                Assert.IsType<SolidColorBrush>(section.Foreground).Color);
            Assert.Equal(14, task.FontSize);
            Assert.Equal(18, task.LineHeight);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);
}
