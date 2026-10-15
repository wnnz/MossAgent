using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ConversationTabControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OwnedButtonsAndPopup_UseExplicitTargetsWithoutAWindowViewModel(bool dark) => _session.DispatchAsync(() =>
    {
        var target = new object();
        object? selected = null;
        object? closed = null;
        var control = new AppConversationTab
        {
            Title = "Very long conversation title for ellipsis and centered icons",
            CommandParameter = target,
            SelectCommand = new RelayCommand<object>(item => selected = item),
            CloseCommand = new RelayCommand<object>(item => closed = item),
            IsSelected = true
        };
        var window = new Window { Content = control, Width = 300, Height = 180, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var select = control.FindControl<AppActionButton>("SelectButton")!;
            var options = control.FindControl<AppIconButton>("OptionsButton")!;
            Assert.Equal(238, control.Bounds.Width);
            Assert.Equal(34, select.Bounds.Height);
            Assert.Equal(24, options.Bounds.Width);
            Assert.Equal(select.TranslatePoint(new Point(0, select.Bounds.Height / 2), control)!.Value.Y,
                options.TranslatePoint(new Point(0, options.Bounds.Height / 2), control)!.Value.Y);
            var label = Assert.Single(select.GetVisualDescendants().OfType<AppIconLabel>());
            Assert.Equal(control.Title, select.Label);
            Assert.Equal(control.Title, Assert.Single(label.GetVisualDescendants().OfType<AppText>()).Text);
            Assert.Equal(160, label.LabelMaxWidth);
            Assert.Same(control.SelectCommand, select.Command);
            Press(select);
            Assert.Same(target, selected);
            var flyout = Assert.IsType<AppFlyout>(options.Flyout);
            flyout.ShowAt(options);
            window.UpdateLayout();
            var surface = Assert.IsType<AppCard>(flyout.Content);
            var close = Assert.IsType<AppButton>(surface.Child);
            Assert.True(flyout.IsOpen);
            Assert.Equal(190, surface.Bounds.Width);
            Assert.Equal(new Thickness(0), close.BorderThickness);
            Assert.Same(control.CloseCommand, close.Command);
            Assert.Same(target, close.CommandParameter);
            Assert.True(close.IsEffectivelyEnabled);
            Assert.True(close.IsFocused);
            Press(close);
            Assert.Same(target, closed);
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("target")]
    [InlineData("select")]
    [InlineData("close")]
    [InlineData("denied")]
    public Task MissingTargetsOrCommands_AreDisabledAndCanExecuteChangesCloseThePopup(string missing) => _session.DispatchAsync(() =>
    {
        var allowed = missing != "denied";
        var closeCommand = new RelayCommand<object>(_ => { }, _ => allowed);
        var control = new AppConversationTab
        {
            CommandParameter = missing == "target" ? null : new object(),
            SelectCommand = missing == "select" ? null : new RelayCommand<object>(_ => { }),
            CloseCommand = missing == "close" ? null : closeCommand,
            IsSelected = true
        };
        var window = new Window { Content = control, Width = 300, Height = 180 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var select = control.FindControl<AppActionButton>("SelectButton")!;
            var options = control.FindControl<AppIconButton>("OptionsButton")!;
            Assert.Equal(missing is not ("target" or "select"), select.IsEffectivelyEnabled);
            Assert.Equal(missing == "select", options.IsEffectivelyEnabled);
            if (missing == "select")
            {
                var flyout = Assert.IsType<AppFlyout>(options.Flyout);
                flyout.ShowAt(options);
                Assert.True(flyout.IsOpen);
                allowed = false;
                closeCommand.NotifyCanExecuteChanged();
                Assert.False(options.IsEffectivelyEnabled);
                Assert.False(flyout.IsOpen);
            }
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("target")]
    [InlineData("context")]
    [InlineData("command")]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("ancestor-hidden")]
    [InlineData("ancestor-disabled")]
    [InlineData("detach")]
    public Task ScopeOrAvailabilityChanges_CloseTheOwnedPopup(string change) => _session.DispatchAsync(() =>
    {
        var control = new AppConversationTab { CommandParameter = new object(), CloseCommand = new RelayCommand<object>(_ => { }), IsSelected = true };
        var host = new StackPanel { Children = { control } };
        var window = new Window { Content = host, Width = 300, Height = 180 };
        window.Show();
        try
        {
            var options = control.FindControl<AppIconButton>("OptionsButton")!;
            var flyout = Assert.IsType<AppFlyout>(options.Flyout);
            flyout.ShowAt(options);
            Assert.True(flyout.IsOpen);
            switch (change)
            {
                case "target": control.CommandParameter = new object(); break;
                case "context": control.DataContext = new object(); break;
                case "command": control.CloseCommand = new RelayCommand<object>(_ => { }); break;
                case "hidden": control.IsVisible = false; break;
                case "disabled": control.IsEnabled = false; break;
                case "ancestor-hidden": host.IsVisible = false; break;
                case "ancestor-disabled": host.IsEnabled = false; break;
                case "detach": host.Children.Clear(); break;
            }
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DelayedDismissal_DoesNotCloseANewlyOwnedPopup() => _session.DispatchAsync(() =>
    {
        var control = new AppConversationTab { CommandParameter = new object(), IsSelected = true };
        var window = new Window { Content = control, Width = 300, Height = 180 };
        var options = control.FindControl<AppIconButton>("OptionsButton")!;
        var flyout = Assert.IsType<AppFlyout>(options.Flyout);
        var executions = 0;
        control.CloseCommand = new RelayCommand<object>(_ =>
        {
            executions++;
            control.CommandParameter = new object();
            flyout.ShowAt(options);
        });
        window.Show();
        try
        {
            flyout.ShowAt(options);
            Press(control.FindControl<AppButton>("CloseButton")!);
            Assert.Equal(1, executions);
            Dispatcher.UIThread.RunJobs();
            Assert.True(flyout.IsOpen);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Reattachment_ObservesOnlyTheCurrentAncestorsAndCommands() => _session.DispatchAsync(() =>
    {
        var oldAllowed = true;
        var currentAllowed = true;
        var oldCommand = new RelayCommand<object>(_ => { }, _ => oldAllowed);
        var currentCommand = new RelayCommand<object>(_ => { }, _ => currentAllowed);
        var control = new AppConversationTab { CommandParameter = new object(), CloseCommand = oldCommand, IsSelected = true };
        var oldHost = new StackPanel { Children = { control } };
        var currentHost = new StackPanel();
        var window = new Window { Content = new StackPanel { Children = { oldHost, currentHost } }, Width = 300, Height = 180 };
        window.Show();
        try
        {
            var options = control.FindControl<AppIconButton>("OptionsButton")!;
            for (var cycle = 0; cycle < 3; cycle++)
            {
                oldHost.Children.Clear();
                Assert.False(options.IsEffectivelyEnabled);
                currentHost.Children.Add(control);
                Assert.True(options.IsEffectivelyEnabled);
                currentHost.Children.Clear();
                oldHost.Children.Add(control);
            }
            oldHost.Children.Clear();
            currentHost.Children.Add(control);
            control.CloseCommand = currentCommand;
            oldAllowed = false;
            oldCommand.NotifyCanExecuteChanged();
            oldHost.IsEnabled = false;
            oldHost.IsVisible = false;
            Assert.True(options.IsEffectivelyEnabled);
            var flyout = Assert.IsType<AppFlyout>(options.Flyout);
            flyout.ShowAt(options);
            currentAllowed = false;
            currentCommand.NotifyCanExecuteChanged();
            Assert.False(options.IsEffectivelyEnabled);
            Assert.False(flyout.IsOpen);
            currentAllowed = true;
            currentCommand.NotifyCanExecuteChanged();
            Assert.True(options.IsEffectivelyEnabled);
            flyout.ShowAt(options);
            currentHost.IsVisible = false;
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    internal static void Press(AppButton button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
