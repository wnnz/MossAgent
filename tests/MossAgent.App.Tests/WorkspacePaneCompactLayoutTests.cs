using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspacePaneCompactLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 470, true)]
    [InlineData(true, 470, true)]
    [InlineData(false, 643, true)]
    [InlineData(true, 643, true)]
    [InlineData(false, 644, false)]
    [InlineData(true, 644, false)]
    public Task InsufficientWidthUsesFullInspectorInsteadOfAnUnusableChat(bool dark, int width, bool fullWidth) =>
        WithPane(dark, (window, panes, draft) =>
        {
            Resize(window, width);
            var inspector = panes.InspectorPane!;
            Assert.Equal(width, window.ClientSize.Width);
            Assert.Equal(!fullWidth, draft.IsEffectivelyVisible);
            Assert.Equal(fullWidth, panes.IsInspectorFullWidth);
            var splitter = Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>());
            Assert.Equal(!fullWidth, splitter.IsEffectivelyVisible);
            if (fullWidth) Assert.Equal(width, inspector.Bounds.Width, 3);
            else Assert.True(draft.Bounds.Width >= panes.MinimumPrimaryWidth);
            Assert.False(panes.IsInspectorExpanded);
            Assert.Equal("unsent compact draft", draft.Text);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CompactRoundTripRestoresThePreferredSplitAndSameControls(bool dark) => WithPane(dark, (window, panes, draft) =>
    {
        var inspector = panes.InspectorPane!;
        var layout = Assert.IsType<Grid>(panes.Content);
        layout.ColumnDefinitions[2].Width = new GridLength(620);
        window.UpdateLayout();
        var preferred = inspector.Bounds.Width;
        Resize(window, 470);
        Assert.False(draft.IsEffectivelyVisible);
        Resize(window, 1600);
        Assert.Equal(preferred, inspector.Bounds.Width, 3);
        Assert.True(draft.IsEffectivelyVisible);
        Assert.Same(draft, panes.PrimaryPane);
        Assert.Same(inspector, panes.InspectorPane);
        Assert.Equal("unsent compact draft", draft.Text);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CompactCloseRestoresChatAndRejectsExpansionWithoutLosingManualPreference(bool dark) =>
        WithPane(dark, (window, panes, draft) =>
        {
            panes.ToggleInspectorExpandedCommand.Execute(null);
            Assert.True(panes.IsInspectorExpanded);
            Resize(window, 470);
            Assert.False(panes.ToggleInspectorExpandedCommand.CanExecute(null));
            panes.ToggleInspectorExpandedCommand.Execute(null);
            Assert.True(panes.IsInspectorExpanded);
            Resize(window, 1600);
            Assert.False(draft.IsEffectivelyVisible);
            panes.ToggleInspectorExpandedCommand.Execute(null);
            window.UpdateLayout();
            Assert.True(draft.IsEffectivelyVisible);
            Resize(window, 470);
            panes.IsInspectorOpen = false;
            window.UpdateLayout();
            Assert.True(draft.IsEffectivelyVisible);
            Assert.Equal(470, draft.Bounds.Width, 3);
            Assert.False(panes.IsInspectorExpanded);
            Assert.False(panes.IsInspectorFullWidth);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualArrangeOverridesAConflictingMeasureAndNotifiesPresentation(bool dark) => WithPane(dark, (window, panes, draft) =>
    {
        var notifications = new List<bool>();
        panes.PropertyChanged += (_, change) =>
        {
            if (change.Property == AppWorkspacePanes.IsInspectorFullWidthProperty)
                notifications.Add(panes.IsInspectorFullWidth);
        };
        panes.InvalidateMeasure();
        panes.Measure(new Size(1600, 601));
        panes.InvalidateArrange();
        panes.Arrange(new Rect(0, 0, 470, 601));
        Assert.True(panes.IsInspectorFullWidth);
        Assert.Equal(470, panes.InspectorPane!.Bounds.Width, 3);
        Assert.False(draft.IsEffectivelyVisible);
        panes.InvalidateMeasure();
        panes.Measure(new Size(470, 601));
        panes.InvalidateArrange();
        panes.Arrange(new Rect(0, 0, 1600, 601));
        Assert.False(panes.IsInspectorFullWidth);
        Assert.True(draft.IsEffectivelyVisible);
        Assert.Equal(new[] { true, false }, notifications);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SharedMinimumChangesDriveTheBreakpointAndDoNotChangeExplicitExpansion(bool dark) => WithPane(dark, (window, panes, draft) =>
    {
        Resize(window, 470);
        Assert.True(panes.IsInspectorFullWidth);
        panes.MinimumPrimaryWidth = 200;
        panes.MinimumInspectorWidth = 240;
        window.UpdateLayout();
        Assert.False(panes.IsInspectorFullWidth);
        Assert.True(draft.Bounds.Width >= 200);
        Resize(window, 443);
        Assert.True(panes.IsInspectorFullWidth);
        Resize(window, 444);
        Assert.False(panes.IsInspectorFullWidth);
        Assert.False(panes.IsInspectorExpanded);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InitiallyCompactHostAttachesTheSamePrimaryOnlyWhenTheInspectorCloses(bool dark) => _session.Dispatch(() =>
    {
        var draft = new AppInput { Text = "initial compact draft" };
        var panes = new AppWorkspacePanes { PrimaryPane = draft, InspectorPane = new AppBorder(), IsInspectorOpen = true };
        var window = new AppWindow
        {
            Width = 470,
            Height = 601,
            Content = panes,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(panes.IsInspectorFullWidth);
            var presenter = Assert.Single(panes.GetVisualDescendants().OfType<AppContentControl>(), control => ReferenceEquals(control.Content, draft));
            Assert.False(presenter.IsVisible);
            Assert.Null(TopLevel.GetTopLevel(draft));
            panes.IsInspectorOpen = false;
            window.UpdateLayout();
            Assert.Same(window, TopLevel.GetTopLevel(draft));
            Assert.True(draft.IsEffectivelyVisible);
            Assert.Equal("initial compact draft", draft.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private Task WithPane(bool dark, Action<AppWindow, AppWorkspacePanes, AppInput> verify) => _session.Dispatch(() =>
    {
        var draft = new AppInput { Text = "unsent compact draft" };
        var panes = new AppWorkspacePanes { PrimaryPane = draft, InspectorPane = new AppBorder(), IsInspectorOpen = true };
        var window = new AppWindow
        {
            Width = 1600,
            Height = 601,
            Content = panes,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); verify(window, panes, draft); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Resize(AppWindow window, double width)
    {
        window.Width = width;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
