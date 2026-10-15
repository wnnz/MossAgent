using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspacePaneArrangeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DockedInspector_UsesArrangedWidthWhenMeasurementWasNarrower(bool dark) => WithPanes(dark, (window, panes) =>
    {
        panes.IsInspectorOpen = true;
        window.UpdateLayout();
        Arrange(panes, 660, 1600);
        var expected = panes.Bounds.Width * panes.DefaultInspectorFraction;
        var layout = Assert.IsType<Grid>(panes.Content);
        Assert.Equal(expected, layout.ColumnDefinitions[2].Width.Value);
        Assert.Equal(expected, layout.ColumnDefinitions[2].ActualWidth);
        Assert.InRange(panes.InspectorPane!.Bounds.Width - expected, -1, 1);
        Assert.InRange(panes.PrimaryPane!.Bounds.Width, 843, 845);
        Assert.Same(panes.InspectorPane, Assert.Single(panes.GetVisualDescendants().OfType<AppContentControl>(),
            presenter => ReferenceEquals(presenter.Content, panes.InspectorPane)).Content);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FloatingInspector_UsesArrangedWidthAndSharedMargins(bool dark) => WithPanes(dark, (window, panes) =>
    {
        Arrange(panes, 220, 1600);
        var inspector = panes.InspectorPane!;
        var placement = inspector.TranslatePoint(default, panes)!.Value;
        Assert.Equal(panes.FloatingInspectorWidth, inspector.Bounds.Width);
        Assert.Equal(panes.FloatingInspectorMargin.Top, placement.Y);
        Assert.Equal(panes.FloatingInspectorMargin.Right, panes.Bounds.Width - placement.X - inspector.Bounds.Width);
        Assert.Equal(panes.Bounds.Width, panes.PrimaryPane!.Bounds.Width);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PreferredWidth_SurvivesMeasurementClampAndActualArrange(bool dark) => WithPanes(dark, (window, panes) =>
    {
        panes.IsInspectorOpen = true;
        window.UpdateLayout();
        Assert.True(Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>()).Focus());
        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        window.KeyRelease(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        window.UpdateLayout();
        var preferred = panes.InspectorPane!.Bounds.Width;
        Arrange(panes, 660, 1600);
        Assert.InRange(panes.InspectorPane.Bounds.Width - preferred, -1, 1);
        Arrange(panes, 1600, 660);
        Assert.InRange(panes.InspectorPane.Bounds.Width, 280, 300);
        Assert.True(panes.PrimaryPane!.Bounds.Width >= panes.MinimumPrimaryWidth);
        Arrange(panes, 660, 1600);
        Assert.InRange(panes.InspectorPane.Bounds.Width - preferred, -1, 1);
    });

    private Task WithPanes(bool dark, Action<AppWindow, AppWorkspacePanes> verify) => _session.Dispatch(() =>
    {
        var panes = new AppWorkspacePanes { PrimaryPane = new AppBorder(), InspectorPane = new AppBorder() };
        var window = new AppWindow
        {
            Width = 1600,
            Height = 820,
            Content = panes,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            verify(window, panes);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Arrange(AppWorkspacePanes panes, double measuredWidth, double arrangedWidth)
    {
        panes.InvalidateMeasure();
        panes.Measure(new Size(measuredWidth, 820));
        panes.InvalidateArrange();
        panes.Arrange(new Rect(0, 0, arrangedWidth, 820));
        Assert.Equal(arrangedWidth, panes.Bounds.Width);
    }
}
