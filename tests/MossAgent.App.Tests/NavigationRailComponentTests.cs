using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NavigationRailComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingCommandsAndHelpContent_DisableOwnedActions(bool dark) => WithRail(dark, (window, rail, parent) =>
    {
        foreach (var name in new[] { "HomeButton", "AuditButton", "SettingsButton", "MoreButton", "HelpButton" })
            Assert.False(rail.FindControl<AppIconButton>(name)!.IsEffectivelyEnabled);
        Assert.False(rail.FindControl<AppIconButton>("AutomationButton")!.IsEffectivelyEnabled);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SettingsSelection_OverridesBackgroundPagesAndRestoresTheirSelection(bool dark) => WithRail(dark, (window, rail, parent) =>
    {
        var home = rail.FindControl<AppIconButton>("HomeButton")!;
        var automation = rail.FindControl<AppIconButton>("AutomationButton")!;
        Assert.Contains("selected", home.Classes);
        rail.IsAutomationsSelected = true;
        Assert.DoesNotContain("selected", home.Classes);
        Assert.Contains("selected", automation.Classes);
        rail.IsSettingsSelected = true;
        Assert.Equal("返回工作区", home.Label);
        Assert.DoesNotContain("selected", home.Classes);
        Assert.DoesNotContain("selected", automation.Classes);
        Assert.False(automation.IsEffectivelyEnabled);
        rail.IsSettingsSelected = false;
        Assert.Equal("新对话", home.Label);
        Assert.Contains("selected", automation.Classes);
        rail.IsAutomationsSelected = false;
        Assert.Contains("selected", home.Classes);
        Assert.DoesNotContain("selected", automation.Classes);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CommandCanExecute_RemainsAuthoritativeAcrossLiveChanges(bool dark) => WithRail(dark, (window, rail, parent) =>
    {
        var allowed = false;
        var calls = 0;
        var command = new RelayCommand(() => calls++, () => allowed);
        rail.NewConversationCommand = command;
        var home = rail.FindControl<AppIconButton>("HomeButton")!;
        Assert.False(home.IsEffectivelyEnabled);
        allowed = true;
        command.NotifyCanExecuteChanged();
        Dispatcher.UIThread.RunJobs();
        Assert.True(home.IsEffectivelyEnabled);
        Activate(home);
        Assert.Equal(1, calls);
        allowed = false;
        command.NotifyCanExecuteChanged();
        Dispatcher.UIThread.RunJobs();
        Assert.False(home.IsEffectivelyEnabled);
        window.IsEnabled = false;
        Assert.False(home.IsEffectivelyEnabled);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MoreMenu_ExecutesOnceAndDismissesWhileHelpInheritsBusinessContext(bool dark) => WithRail(dark, (window, rail, parent) =>
    {
        var calls = 0;
        var context = new object();
        rail.DataContext = context;
        rail.ToggleInspectorCommand = new RelayCommand(() => calls++);
        var help = new AppText { Text = "Help content" };
        rail.HelpContent = help;
        var more = rail.FindControl<AppIconButton>("MoreButton")!;
        Activate(more);
        Assert.True(more.Flyout!.IsOpen);
        Activate(rail.FindControl<AppButton>("InspectorMenuButton")!);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, calls);
        Assert.False(more.Flyout.IsOpen);
        var helpButton = rail.FindControl<AppIconButton>("HelpButton")!;
        Activate(helpButton);
        window.UpdateLayout();
        Assert.True(helpButton.Flyout!.IsOpen);
        Assert.Same(context, help.DataContext);
        help.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Dispatcher.UIThread.RunJobs();
        Assert.False(helpButton.Flyout.IsOpen);
    });

    [Theory]
    [InlineData("command")]
    [InlineData("context")]
    [InlineData("settings-selection")]
    [InlineData("help")]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("ancestor-disabled")]
    [InlineData("ancestor-hidden")]
    [InlineData("detached")]
    public Task OwnershipAndLifetimeChanges_CloseOpenFlyouts(string reason) => WithRail(false, (window, rail, parent) =>
    {
        rail.SettingsCommand = new RelayCommand(() => { });
        var more = rail.FindControl<AppIconButton>("MoreButton")!;
        Activate(more);
        Assert.True(more.Flyout!.IsOpen);
        switch (reason)
        {
            case "command": rail.SettingsCommand = new RelayCommand(() => { }); break;
            case "context": rail.DataContext = new object(); break;
            case "settings-selection": rail.IsSettingsSelected = true; break;
            case "help": rail.HelpContent = new AppText(); break;
            case "hidden": rail.IsVisible = false; break;
            case "disabled": rail.IsEnabled = false; break;
            case "ancestor-disabled": parent.IsEnabled = false; break;
            case "ancestor-hidden": parent.IsVisible = false; break;
            case "detached": parent.Children.Remove(rail); break;
        }
        Dispatcher.UIThread.RunJobs();
        Assert.False(more.Flyout.IsOpen);
    });

    private Task WithRail(bool dark, Action<AppWindow, AppNavigationRail, StackPanel> action) => _session.Dispatch(() =>
    {
        var rail = new AppNavigationRail();
        var parent = new StackPanel { Children = { rail } };
        var window = new AppWindow
        {
            Width = 400,
            Height = 500,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = parent
        };
        window.Show();
        try { window.UpdateLayout(); action(window, rail, parent); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
