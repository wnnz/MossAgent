using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarBrandTypographyTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Brand_UsesSharedHeadingAndOpticalChevronWithoutMovingHeader(bool dark, bool hovered) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var sidebar = new WorkspaceSidebar();
        var window = new AppWindow { Width = 264, Height = 200, Content = sidebar };
        window.Show();
        try
        {
            window.UpdateLayout();
            var brand = Assert.Single(sidebar.GetVisualDescendants().OfType<AppActionButton>(),
                button => button.Classes.Contains("sidebar-brand-trigger"));
            var point = brand.TranslatePoint(new Point(brand.Bounds.Width / 2, brand.Bounds.Height / 2), window)!.Value;
            window.MouseMove(hovered ? point : new Point(250, 190));
            window.UpdateLayout();
            var label = Assert.Single(brand.GetVisualDescendants().OfType<AppText>(), text => text.IsVisible);
            var arrow = Assert.Single(brand.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsVisible);
            Assert.Equal(18, brand.FontSize);
            Assert.Equal(18, label.FontSize);
            Assert.Equal(FontWeight.Bold, brand.FontWeight);
            Assert.Equal(FontWeight.Bold, label.FontWeight);
            Assert.Equal("MossAgent", label.Text);
            Assert.Equal(6, arrow.Size);
            Assert.Equal(1, arrow.StrokeThickness);
            Assert.Equal(new Matrix(1, 0, 0, 1, 0, 2.5), arrow.RenderTransform!.Value);
            Assert.Equal(32, brand.Bounds.Height);
            Assert.True(brand.IsEffectivelyEnabled);
            Assert.True(brand.Focus(NavigationMethod.Tab));
            var action = Assert.Single(sidebar.GetVisualDescendants().OfType<AppActionButton>(),
                button => button.Classes.Contains("sidebar-action-button"));
            Assert.Equal(14, action.FontSize);
            Assert.Equal(FontWeight.Normal, action.FontWeight);
            Assert.Equal(30, action.Bounds.Height);
            Assert.Equal(32, sidebar.GetVisualDescendants().OfType<AppSidebarSearch>().Single().Bounds.Height);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BrandVariant_DoesNotResizeOtherTrailingIconsOrLoseLiveLabel(bool dark) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var geometry = Geometry.Parse("M 6 9 L 12 15 L 18 9");
        var brand = new AppActionButton { Label = "MossAgent", TrailingIconData = geometry };
        brand.Classes.AddRange(["ghost", "sidebar-brand-trigger"]);
        var ordinary = new AppActionButton { Label = "ordinary", TrailingIconData = geometry };
        var window = new AppWindow { Width = 264, Height = 120, Content = new StackPanel { Children = { brand, ordinary } } };
        window.Show();
        try
        {
            window.UpdateLayout();
            var ordinaryArrow = Assert.Single(ordinary.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsVisible);
            Assert.Equal(10, ordinaryArrow.Size);
            Assert.Equal(1.2, ordinaryArrow.StrokeThickness);
            Assert.Null(ordinaryArrow.RenderTransform);
            brand.Label = "新项目";
            window.UpdateLayout();
            var text = Assert.Single(brand.GetVisualDescendants().OfType<AppText>(), candidate => candidate.IsVisible);
            Assert.Equal("新项目", text.Text);
            Assert.Equal(18, text.FontSize);
            Assert.Equal(FontWeight.Bold, text.FontWeight);
            Assert.Equal(13, ordinary.FontSize);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
