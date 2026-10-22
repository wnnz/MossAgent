using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarFidelityLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(AgentTaskStatus.Running, true)]
    [InlineData(AgentTaskStatus.WaitingForApproval, false)]
    [InlineData(AgentTaskStatus.Completed, false)]
    [InlineData(AgentTaskStatus.Interrupted, false)]
    [InlineData(AgentTaskStatus.Failed, false)]
    public void ActivityMarker_UsesTheTaskSnapshotNotItsSelection(AgentTaskStatus status, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), Guid.NewGuid(), "activity", ApprovalPolicy.ReadOnly, status, now, now, null);
        var navigation = new WorkspaceTaskNavigationViewModel(task) { IsSelected = true };
        Assert.Equal(expected, navigation.IsRunning);
        navigation.IsSelected = false;
        Assert.Equal(expected, navigation.IsRunning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RunningMarker_HandsItsSlotToActionsAndClosesStaleMenus(bool dark) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var selected = 0;
        var row = new AppTaskNavigationRow
        {
            Title = "running task",
            IsRunning = true,
            CommandParameter = new object(),
            SelectCommand = new RelayCommand(() => selected++)
        };
        var outside = new AppButton { Content = "outside" };
        Grid.SetRow(outside, 1);
        var window = new AppWindow
        {
            Width = 264,
            Height = 120,
            Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { row, outside } }
        };
        window.Show();
        try
        {
            outside.Focus();
            window.MouseMove(new Point(260, 115));
            window.UpdateLayout();
            var activity = Assert.Single(row.GetVisualDescendants().OfType<AppIcon>(),
                icon => icon.Classes.Contains("sidebar-task-activity"));
            var options = row.FindControl<AppIconButton>("TaskOptionsButton")!;
            Assert.True(activity.IsVisible);
            Assert.Equal(12, activity.Size);
            Assert.Equal(1, activity.Opacity);
            Assert.NotNull(activity.RenderTransform);
            Assert.Equal(0, options.Opacity);
            Assert.True(options.Focus(NavigationMethod.Tab));
            options.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            options.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            window.UpdateLayout();
            Assert.True(row.IsOptionsOpen);
            Assert.Equal(0, activity.Opacity);
            Assert.Equal(1, options.Opacity);
            row.CommandParameter = new object();
            Assert.False(row.IsOptionsOpen);
            row.IsRunning = false;
            Assert.False(activity.IsVisible);
            Assert.Equal(0, selected);
        }
        finally
        {
            window.Close();
            Assert.False(row.IsOptionsOpen);
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public void SharedAnimationPrimitives_DoNotPermitNativeVisualControls()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedVisualControls");
        var query = Assert.Single(target.Elements("XmlPeek")).Attribute("Query")!.Value;
        var markup = XDocument.Parse("""
            <Grid xmlns="https://github.com/avaloniaui">
              <LinearGradientBrush><GradientStop /></LinearGradientBrush>
              <RotateTransform /><TranslateTransform /><Animation><KeyFrame /></Animation>
              <Button /><TextBlock /><Path /><Border /><ScrollViewer />
            </Grid>
            """);
        Assert.Equal(["Button", "TextBlock", "Path", "Border", "ScrollViewer"],
            markup.XPathSelectElements(query).Select(element => element.Name.LocalName));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActivityAnimation_DoesNotOverwriteOtherIconTransforms(bool dark, bool running) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var row = new AppTaskNavigationRow { Title = "task", IsRunning = running };
        var translated = new AppIcon { Classes = { "ui-icon" } };
        var untransformed = new AppIcon { Classes = { "ui-icon" } };
        translated.SetValue(Visual.RenderTransformProperty, new TranslateTransform(-0.5, -0.5),
            Avalonia.Data.BindingPriority.Style);
        var window = new AppWindow
        {
            Width = 264,
            Height = 150,
            Content = new StackPanel { Children = { row, translated, untransformed } }
        };
        window.Show();
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var activity = Assert.Single(row.GetVisualDescendants().OfType<AppIcon>(),
                icon => icon.Classes.Contains("sidebar-task-activity"));
            Assert.Equal(12, activity.Size);
            Assert.Equal(running, activity.IsVisible);
            if (running) AssertHasRotation(activity);
            Assert.Equal(new Matrix(1, 0, 0, 1, -0.5, -0.5), translated.RenderTransform!.Value);
            Assert.Null(untransformed.RenderTransform);
            row.IsRunning = !running;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(!running, activity.IsVisible);
            if (!running) AssertHasRotation(activity);
            Assert.Equal(new Matrix(1, 0, 0, 1, -0.5, -0.5), translated.RenderTransform.Value);
            Assert.Null(untransformed.RenderTransform);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertHasRotation(AppIcon activity)
    {
        var rotations = activity.RenderTransform switch
        {
            RotateTransform rotation => new[] { rotation },
            TransformGroup group => group.Children.OfType<RotateTransform>().ToArray(),
            _ => []
        };
        Assert.Single(rotations);
    }

    [Fact]
    public void NavigationSections_ShareOneUnboundedScrollFlow()
    {
        var document = XDocument.Load(Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        var pinned = Assert.Single(document.Descendants(), element =>
            element.Attribute("ItemsSource")?.Value == "{Binding TaskManagement.PinnedTasks.Items}");
        var projects = Assert.Single(document.Descendants(), element =>
            element.Attribute("Classes")?.Value == "sidebar-project-list");
        var recent = Assert.Single(document.Descendants(), element =>
            element.Attribute("ItemsSource")?.Value == "{Binding RecentTasks.Items}");
        var scroll = Assert.Single(pinned.Ancestors(), element => element.Name.LocalName == "AppScrollViewer");
        Assert.Contains(scroll, projects.Ancestors());
        Assert.Contains(scroll, recent.Ancestors());
        Assert.Null(scroll.Attribute("MaxHeight"));
        Assert.Contains(scroll.Descendants(), element =>
            element.Attribute("Classes")?.Value.Contains("projects-section-header") == true);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task LongTitles_UseAvailableWidthUntilActionsAreRevealed(bool dark, bool projectTask) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var row = new AppTaskNavigationRow
        {
            Title = string.Concat(Enumerable.Repeat("侧边栏长标题", 30)),
            IsProjectTask = projectTask,
            SelectCommand = new RelayCommand(() => { }),
            CommandParameter = new object()
        };
        var outside = new AppButton { Content = "outside" };
        Grid.SetRow(outside, 1);
        var window = new AppWindow
        {
            Width = 264,
            Height = 120,
            Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { row, outside } }
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            outside.Focus();
            window.MouseMove(new Point(260, 115));
            var select = row.FindControl<AppButton>("SelectTaskButton")!;
            var options = row.FindControl<AppIconButton>("TaskOptionsButton")!;
            var title = Assert.Single(select.GetVisualDescendants().OfType<AppText>());
            var restingRight = title.TranslatePoint(new Point(title.Bounds.Width, 0), row)!.Value.X;
            Assert.Equal(TextTrimming.None, title.TextTrimming);
            Assert.True(title.ClipToBounds);
            Assert.NotNull(title.OpacityMask);
            Assert.Equal(0, options.Opacity);
            Assert.Equal(row.Bounds.Width - 8, restingRight, precision: 3);
            window.MouseMove(select.TranslatePoint(new Point(60, select.Bounds.Height / 2), window)!.Value);
            window.UpdateLayout();
            var actionLeft = options.TranslatePoint(default, row)!.Value.X;
            var hoveredRight = title.TranslatePoint(new Point(title.Bounds.Width, 0), row)!.Value.X;
            Assert.True(hoveredRight <= actionLeft);
            Assert.True(restingRight > hoveredRight);
            Assert.Equal(1, options.Opacity);
            Assert.Equal(30, row.Bounds.Height);
            Assert.True(options.Focus(NavigationMethod.Tab));
            window.MouseMove(new Point(260, 115));
            window.UpdateLayout();
            Assert.Equal(1, options.Opacity);
            Assert.True(title.TranslatePoint(new Point(title.Bounds.Width, 0), row)!.Value.X <= actionLeft);
            outside.Focus();
            window.MouseMove(new Point(260, 115));
            row.Title = "短对话";
            window.UpdateLayout();
            Assert.Equal(restingRight, title.TranslatePoint(new Point(title.Bounds.Width, 0), row)!.Value.X, precision: 3);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

}
