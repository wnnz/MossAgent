using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NavigationActionStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("task", "Light")]
    [InlineData("task", "Dark")]
    [InlineData("project", "Light")]
    [InlineData("project", "Dark")]
    [InlineData("section", "Light")]
    [InlineData("section", "Dark")]
    [InlineData("tab", "Light")]
    [InlineData("tab", "Dark")]
    public Task HiddenActions_DoNotInterceptPointerAndRevealWithTheirRow(string kind, string theme) =>
        WithWindow(kind, theme, (window, row, select, action, outside) =>
        {
            outside.Focus();
            window.MouseMove(new Point(window.Width - 10, window.Height - 10));
            AssertHidden(action);
            Assert.False(IsActionHit(window, action));
            window.MouseMove(Center(select, window));
            AssertRevealed(action);
            window.MouseMove(Center(action, window));
            AssertRevealed(action);
            Assert.True(IsActionHit(window, action));
            window.MouseMove(new Point(window.Width - 10, window.Height - 10));
            AssertHidden(action);
        });

    [Theory]
    [InlineData("task", "Light")]
    [InlineData("task", "Dark")]
    [InlineData("project", "Light")]
    [InlineData("project", "Dark")]
    [InlineData("section", "Light")]
    [InlineData("section", "Dark")]
    [InlineData("tab", "Light")]
    [InlineData("tab", "Dark")]
    public Task KeyboardFocus_RevealsActionsWithoutSelectingTheRow(string kind, string theme) =>
        WithWindow(kind, theme, (window, row, select, action, outside) =>
        {
            var selected = 0;
            select.Command = new RelayCommand(() => selected++);
            Assert.True(select.Focus(NavigationMethod.Tab));
            AssertRevealed(action);
            Assert.True(action.Focus(NavigationMethod.Tab));
            AssertRevealed(action);
            Assert.Equal(0, selected);
            Assert.True(outside.Focus());
            AssertHidden(action);
        });

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task SelectedTab_KeepsOptionsVisibleUntilDeselected(string theme) =>
        WithWindow("tab", theme, (window, row, select, action, outside) =>
        {
            row.Classes.Add("selected");
            AssertRevealed(action);
            Assert.True(outside.Focus());
            AssertRevealed(action);
            row.Classes.Remove("selected");
            AssertHidden(action);
        });

    [Theory]
    [InlineData("project", "Light")]
    [InlineData("project", "Dark")]
    [InlineData("tab", "Light")]
    [InlineData("tab", "Dark")]
    public Task DisabledAction_RemainsDisabledWhenRevealed(string kind, string theme) =>
        WithWindow(kind, theme, (window, row, select, action, outside) =>
        {
            var invoked = 0;
            action.Command = new RelayCommand(() => invoked++, () => false);
            select.Focus(NavigationMethod.Tab);
            AssertRevealed(action);
            Assert.False(action.IsEffectivelyEnabled);
            Activate(action);
            Assert.Equal(0, invoked);
        });

    [Theory]
    [InlineData("task", "Light")]
    [InlineData("task", "Dark")]
    [InlineData("tab", "Light")]
    [InlineData("tab", "Dark")]
    [InlineData("section", "Light")]
    [InlineData("section", "Dark")]
    public Task OpenMenu_KeepsAnchorVisibleWithoutRowFocusAndDismissesAfterAction(string kind, string theme) =>
        WithWindow(kind, theme, (window, row, select, action, outside) =>
        {
            var invoked = 0;
            var menu = new AppButton { Content = "Action", Command = new RelayCommand(() => invoked++) };
            var flyout = new AppFlyout { Content = new AppCard { Child = menu }, DismissOnButtonClick = true };
            action.Flyout = flyout;
            action.Focus(NavigationMethod.Tab);
            Activate(action);
            window.UpdateLayout();
            Assert.True(flyout.IsOpen);
            Assert.True(menu.Focus());
            Assert.Contains(":flyout-open", action.Classes);
            AssertRevealed(action);
            Assert.True(outside.Focus());
            Assert.False(select.IsKeyboardFocusWithin);
            Assert.False(action.IsKeyboardFocusWithin);
            Assert.True(flyout.IsOpen);
            AssertRevealed(action);
            Assert.True(menu.Focus());
            Activate(menu);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, invoked);
            Assert.False(flyout.IsOpen);
            outside.Focus();
            AssertHidden(action);
        });

    [Theory]
    [InlineData("task", "Light")]
    [InlineData("task", "Dark")]
    [InlineData("tab", "Light")]
    [InlineData("tab", "Dark")]
    [InlineData("section", "Light")]
    [InlineData("section", "Dark")]
    public Task Escape_DismissesMenuWithoutExecutingAction(string kind, string theme) =>
        WithWindow(kind, theme, (window, row, select, action, outside) =>
        {
            var invoked = 0;
            var menu = new AppButton { Content = "Action", Command = new RelayCommand(() => invoked++) };
            var flyout = new AppFlyout { Content = new AppCard { Child = menu }, DismissOnButtonClick = true };
            action.Flyout = flyout;
            action.Focus(NavigationMethod.Tab);
            Activate(action);
            window.UpdateLayout();
            Assert.True(flyout.IsOpen);
            Assert.True(menu.Focus());
            menu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
            Assert.DoesNotContain(":flyout-open", action.Classes);
            Assert.Equal(0, invoked);
            outside.Focus();
            AssertHidden(action);
        });

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task ProjectToggle_ChangesItsOwnStateThroughKeyboard(string theme) =>
        WithWindow("project", theme, (window, row, select, action, outside) =>
        {
            var parameter = new object();
            action.CommandParameter = parameter;
            action.Command = new RelayCommand<object?>(target =>
            {
                Assert.Same(parameter, target);
                action.IsAlternateIcon = !action.IsAlternateIcon;
            });
            Assert.True(action.Focus(NavigationMethod.Tab));
            AssertRevealed(action);
            Activate(action);
            Assert.True(action.IsAlternateIcon);
            Activate(action);
            Assert.False(action.IsAlternateIcon);
        });

    private Task WithWindow(string kind, string theme,
        Action<AppWindow, Control, AppButton, AppIconButton, AppButton> test) => _session.Dispatch(() =>
    {
        var (row, select, action) = CreateRow(kind);
        var outside = new AppButton { Content = "Outside" };
        var window = new AppWindow
        {
            Width = 500,
            Height = 300,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { row, outside } }
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            outside.Focus();
            window.MouseMove(new Point(490, 290));
            test(window, row, select, action, outside);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static (Control Row, AppButton Select, AppIconButton Action) CreateRow(string kind)
    {
        if (kind == "task")
        {
            var task = new AppTaskNavigationRow { Title = "Conversation" };
            return (task, task.FindControl<AppButton>("SelectTaskButton")!,
                task.FindControl<AppIconButton>("TaskOptionsButton")!);
        }

        var select = new AppActionButton { Label = "Navigation", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var action = new AppIconButton { Label = "Options", Classes = { "icon-compact" } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { select, action } };
        Grid.SetColumn(action, 1);
        if (kind == "project")
        {
            grid.Classes.Add("project-navigation-row");
            select.Classes.Add("sidebar-project-row");
            action.Classes.Add("project-toggle");
            return (grid, select, action);
        }

        var border = new AppBorder { Child = grid };
        border.Classes.Add(kind == "tab" ? "conversation-tab-host" : "projects-section-header");
        select.Classes.Add(kind == "tab" ? "conversation-tab" : "ghost");
        action.Classes.Add(kind == "tab" ? "conversation-tab-options" : "project-header-action");
        return (border, select, action);
    }

    private static Point Center(Control control, Control window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static bool IsActionHit(AppWindow window, AppButton action) =>
        window.InputHitTest(Center(action, window)) is Visual hit
        && (ReferenceEquals(hit, action) || action.IsVisualAncestorOf(hit));

    private static void AssertHidden(AppButton action)
    {
        Assert.Equal(0, action.Opacity);
        Assert.False(action.IsHitTestVisible);
    }

    private static void AssertRevealed(AppButton action)
    {
        Assert.Equal(1, action.Opacity);
        Assert.True(action.IsHitTestVisible);
    }

    private static void Activate(AppButton button)
    {
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
