using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectNavigationRowLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task FiniteRow_CentersIconsAndLabelWithoutHidingExpansion(bool dark, bool longTitle) => WithRow(dark,
        (window, row, select, toggle, outside) =>
        {
            row.Title = longTitle ? string.Concat(Enumerable.Repeat("长项目名称", 80)) : "MossAgent";
            window.UpdateLayout();
            var label = Assert.Single(select.GetVisualDescendants().OfType<AppText>(), item => item.IsVisible);
            Assert.Equal(13, Assert.Single(select.GetVisualDescendants().OfType<AppIconLabel>()).Spacing);
            var icon = Assert.Single(select.GetVisualDescendants().OfType<AppIcon>(), item => item.IsVisible);
            var arrow = Assert.Single(toggle.GetVisualDescendants().OfType<AppIcon>());
            Assert.Equal(31, row.Bounds.Height);
            Assert.Equal(11, icon.Bounds.Width);
            Assert.Equal(12, arrow.Bounds.Width);
            Assert.Equal(new Matrix(1, 0, 0, 1, 2.5, -1.5), icon.RenderTransform!.Value);
            Assert.Null(label.RenderTransform);
            Assert.Equal(Center(label, row).Y - 1.5, Center(icon, row).Y, precision: 6);
            Assert.Equal(Center(label, row).Y, Center(arrow, row).Y, precision: 6);
            var labelBounds = new Rect(label.TranslatePoint(default, row)!.Value, label.Bounds.Size);
            var actionBounds = new Rect(toggle.TranslatePoint(default, row)!.Value, toggle.Bounds.Size);
            Assert.True(labelBounds.Right <= actionBounds.Left);
            Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
            Assert.Equal(row.Title, ToolTip.GetTip(select));
            outside.Focus();
            window.MouseMove(new Point(270, 190));
            Assert.Equal(0, toggle.Opacity);
            Assert.False(toggle.IsHitTestVisible);
            window.MouseMove(Center(select, window));
            Assert.Equal(1, toggle.Opacity);
            Assert.True(toggle.IsHitTestVisible);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Expansion_ChangesTheFolderAndOnlyInvokesItsOwnCommand(bool dark) => WithRow(dark,
        (window, row, select, toggle, outside) =>
        {
            var selected = 0;
            var expanded = 0;
            row.SelectCommand = new RelayCommand(() => selected++);
            row.ToggleExpandedCommand = new RelayCommand(() => { expanded++; row.IsExpanded = !row.IsExpanded; });
            var closed = select.IconData;
            Assert.NotNull(closed);
            Assert.True(toggle.Focus(NavigationMethod.Tab));
            Activate(toggle);
            Assert.True(row.IsExpanded);
            Assert.True(toggle.IsAlternateIcon);
            Assert.NotSame(closed, select.IconData);
            window.UpdateLayout();
            var icon = Assert.Single(select.GetVisualDescendants().OfType<AppIcon>(), item => item.IsVisible);
            Assert.Equal(13, icon.Size);
            Assert.Equal(new Matrix(1, 0, 0, 1, 1.5, -1.5), icon.RenderTransform!.Value);
            Assert.Equal(new Size(20, 17), select.IconData!.Bounds.Size);
            Assert.Equal(11, Assert.Single(select.GetVisualDescendants().OfType<AppIconLabel>()).Spacing);
            Assert.False(row.IsSummaryOpen);
            Activate(toggle);
            Assert.False(row.IsExpanded);
            Assert.Same(closed, select.IconData);
            Assert.Equal(new Size(18, 18), select.IconData!.Bounds.Size);
            Assert.Equal(11, icon.Size);
            Assert.Equal(new Matrix(1, 0, 0, 1, 2.5, -1.5), icon.RenderTransform!.Value);
            Assert.Equal(13, Assert.Single(select.GetVisualDescendants().OfType<AppIconLabel>()).Spacing);
            Assert.Equal(2, expanded);
            Assert.Equal(0, selected);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Commands_TrackTargetsAndRespectCanExecuteAndMissingInputs(bool dark) => WithRow(dark,
        (window, row, select, toggle, outside) =>
        {
            object? selected = null;
            var allowed = true;
            var command = new RelayCommand<object?>(target => selected = target, _ => allowed);
            row.SelectCommand = command;
            row.ActionTarget = new object();
            Activate(select);
            Assert.Same(row.ActionTarget, selected);
            row.ActionTarget = new object();
            Activate(select);
            Assert.Same(row.ActionTarget, selected);
            allowed = false;
            command.NotifyCanExecuteChanged();
            Assert.False(select.IsEffectivelyEnabled);
            var previous = selected;
            Activate(select);
            Assert.Same(previous, selected);
            row.ActionTarget = null;
            Assert.False(toggle.IsEffectivelyEnabled);
            row.ActionTarget = new object();
            row.ToggleExpandedCommand = null;
            Assert.False(toggle.IsEffectivelyEnabled);
            row.SelectCommand = null;
            Assert.False(select.IsEffectivelyEnabled);
            Assert.Null(select.Flyout);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Summary_RetainsBusinessDataContextAndTwoWayStateAcrossDetachment(bool dark) => WithRow(dark,
        (window, row, select, toggle, outside) =>
        {
            var project = new ProjectProfile(Guid.NewGuid(), "summary-project", @"C:\fixture", [@"C:\fixture"], DateTimeOffset.UtcNow);
            var navigation = new WorkspaceProjectNavigationViewModel(project);
            row.DataContext = navigation;
            row.ActionTarget = project;
            row.Bind(AppProjectNavigationRow.IsSummaryOpenProperty,
                new Binding(nameof(navigation.IsSummaryOpen)) { Source = navigation, Mode = BindingMode.TwoWay });
            var text = new AppText();
            text.Bind(TextBlock.TextProperty, new Binding("Project.Name"));
            row.SummaryContent = new AppCard { Child = text };
            var flyout = Assert.IsType<AppFlyout>(select.Flyout);
            Activate(select);
            window.UpdateLayout();
            Assert.True(flyout.IsOpen);
            Assert.True(navigation.IsSummaryOpen);
            Assert.Equal(project.Name, text.Text);
            var stack = Assert.IsType<StackPanel>(window.Content);
            stack.Children.Remove(row);
            Assert.False(flyout.IsOpen);
            Assert.False(navigation.IsSummaryOpen);
            stack.Children.Insert(0, row);
            window.UpdateLayout();
            Activate(select);
            Assert.True(navigation.IsSummaryOpen);
            row.ActionTarget = new object();
            Assert.False(flyout.IsOpen);
            Assert.False(navigation.IsSummaryOpen);
            row.SummaryContent = null;
            Assert.Null(select.Flyout);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SidebarComposeIcon_UsesSharedMeasuredSize(bool dark) => _session.Dispatch(() =>
    {
        var button = new AppActionButton { Classes = { "sidebar-action-button" }, Label = "新对话" };
        var window = new AppWindow
        {
            Width = 280,
            Height = 100,
            Content = button,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            button.IconData = Geometry.Parse("M 2 2 L 22 22 M 2 22 L 22 2");
            window.UpdateLayout();
            var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>(), item => item.IsVisible);
            Assert.Equal(11, icon.Bounds.Width);
            Assert.Equal(1, icon.StrokeThickness);
            Assert.Equal(new Matrix(1, 0, 0, 1, 0.5, -1), icon.RenderTransform!.Value);
            Assert.Equal(11, Assert.Single(button.GetVisualDescendants().OfType<AppIconLabel>()).Spacing);
            Assert.Equal(new Thickness(0, 2, 0, 1), button.Margin);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private Task WithRow(bool dark, Action<AppWindow, AppProjectNavigationRow, AppActionButton, AppIconButton, AppButton> action) =>
        _session.Dispatch(() =>
        {
            var row = new AppProjectNavigationRow
            {
                Title = "MossAgent",
                ActionTarget = new object(),
                SelectCommand = new RelayCommand(() => { }),
                ToggleExpandedCommand = new RelayCommand(() => { })
            };
            var outside = new AppButton { Content = "Outside" };
            var window = new AppWindow
            {
                Width = 280,
                Height = 200,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new StackPanel { Children = { row, outside } }
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                action(window, row, row.FindControl<AppActionButton>("SelectProjectButton")!,
                    row.FindControl<AppIconButton>("ToggleProjectButton")!, outside);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static Point Center(Control control, Control relative) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), relative)!.Value;

    private static void Activate(AppButton button)
    {
        button.Focus();
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        Dispatcher.UIThread.RunJobs();
    }
}
