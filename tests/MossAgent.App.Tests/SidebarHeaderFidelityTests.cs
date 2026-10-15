using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarHeaderFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(false, 1280, 820)]
    [InlineData(false, 1920, 1032)]
    [InlineData(true, 980, 640)]
    [InlineData(true, 1280, 820)]
    [InlineData(true, 1920, 1032)]
    public Task ActualSidebar_HeaderIconsShareCompactGeometryAndRetainTheirActions(bool dark, double width, double height) =>
        WithWindow(dark, width, height, (window, fixture) =>
        {
            var search = Assert.Single(window.GetVisualDescendants().OfType<AppSidebarSearch>());
            var trigger = search.FindControl<AppIconButton>("SearchTrigger")!;
            var audit = Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
                button => button.Classes.Contains("sidebar-header-icon") && !button.Classes.Contains("sidebar-search-trigger"));
            Assert.Same(fixture.Main.SelectInspectorTabCommand, audit.Command);
            Assert.Equal("audit", audit.CommandParameter);
            Assert.Same(fixture.Workspace.SidebarSearch.ClearCommand, search.ClearCommand);
            foreach (var button in new[] { audit, trigger })
            {
                Assert.Equal(new Size(32, 32), button.Bounds.Size);
                var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
                Assert.Equal(new Size(11, 11), icon.Bounds.Size);
                Assert.Equal(button == trigger ? 1.2 : 1, icon.StrokeThickness);
                Assert.Equal(0.7, icon.Opacity);
                Assert.Equal(new Matrix(1, 0, 0, 1, 0.5, 2.5), icon.RenderTransform!.Value);
                Assert.Equal(icon.Data!.Bounds.Width, icon.Data.Bounds.Height, precision: 6);
                Assert.Equal(Center(button, window) + new Vector(0, 2), Center(icon, window));
            }
            Assert.Equal(Center(audit, window).Y, Center(trigger, window).Y, precision: 6);
            Assert.Equal(32, Center(trigger, window).X - Center(audit, window).X, precision: 6);
            Assert.Empty(fixture.Server.Requests);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HeaderAppearance_DoesNotChangeGeneralIconButtons(bool dark) => _session.Dispatch(() =>
    {
        var geometry = Geometry.Parse("M 3 8 L 21 8 M 10 2 L 10 20");
        var button = new AppIconButton { IconData = geometry, Label = "general icon" };
        var window = new AppWindow
        {
            Width = 300,
            Height = 100,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = button
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
            Assert.Equal(new Size(14, 14), icon.Bounds.Size);
            Assert.Equal(1.2, icon.StrokeThickness);
            Assert.Equal(1, icon.Opacity);
            Assert.Null(icon.RenderTransform);
            Assert.Same(geometry, icon.Data);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private Task WithWindow(bool dark, double width, double height,
        Action<MainWindow, WorkspaceModelSelectionFixture> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = width,
            Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); action(window, fixture); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static Point Center(Control control, Control relative) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), relative)!.Value;
}
