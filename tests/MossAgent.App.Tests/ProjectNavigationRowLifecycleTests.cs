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
public sealed class ProjectNavigationRowLifecycleTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "context")]
    [InlineData(true, "context")]
    [InlineData(false, "select")]
    [InlineData(true, "select")]
    [InlineData(false, "expand")]
    [InlineData(true, "expand")]
    [InlineData(false, "target")]
    [InlineData(true, "target")]
    [InlineData(false, "content")]
    [InlineData(true, "content")]
    [InlineData(false, "hidden")]
    [InlineData(true, "hidden")]
    [InlineData(false, "disabled")]
    [InlineData(true, "disabled")]
    public Task OwnershipChanges_CloseTheOldSummary(bool dark, string change) => WithOpenRow(dark, (window, host, row, button, popup) =>
    {
        switch (change)
        {
            case "context": row.DataContext = new object(); break;
            case "select": row.SelectCommand = new RelayCommand(() => { }); break;
            case "expand": row.ToggleExpandedCommand = new RelayCommand(() => { }); break;
            case "target": row.ActionTarget = new object(); break;
            case "content": row.SummaryContent = new AppText { Text = "replacement" }; break;
            case "hidden": row.IsVisible = false; break;
            case "disabled": row.IsEnabled = false; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Dispatcher.UIThread.RunJobs();
        Assert.False(popup.IsOpen);
        Assert.False(row.IsSummaryOpen);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task UnavailableAncestor_ClosesTheSummary(bool dark, bool hide) => WithOpenRow(dark, (window, host, row, button, popup) =>
    {
        if (hide) host.IsVisible = false;
        else host.IsEnabled = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(popup.IsOpen);
        Assert.False(row.IsSummaryOpen);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CommandRejection_ClosesTheSummaryWithoutInvokingAnything(bool dark) => WithOpenRow(dark, (window, host, row, button, popup) =>
    {
        popup.Hide();
        var allowed = true;
        var invoked = 0;
        var command = new RelayCommand(() => invoked++, () => allowed);
        row.SelectCommand = command;
        OpenSummary(button, popup);
        var before = invoked;
        allowed = false;
        command.NotifyCanExecuteChanged();
        Dispatcher.UIThread.RunJobs();
        Assert.False(button.IsEffectivelyEnabled);
        Assert.False(popup.IsOpen);
        Assert.False(row.IsSummaryOpen);
        Activate(button);
        Assert.Equal(before, invoked);
    });

    private Task WithOpenRow(bool dark, Action<AppWindow, StackPanel, AppProjectNavigationRow, AppActionButton, AppFlyout> action) =>
        _session.Dispatch(() =>
        {
            var row = new AppProjectNavigationRow
            {
                Title = "project",
                ActionTarget = new object(),
                SelectCommand = new RelayCommand(() => { }),
                ToggleExpandedCommand = new RelayCommand(() => { }),
                SummaryContent = new AppCard { Child = new AppText { Text = "summary" } }
            };
            var host = new StackPanel { Children = { row } };
            var window = new AppWindow
            {
                Width = 280,
                Height = 200,
                Content = host,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var button = row.FindControl<AppActionButton>("SelectProjectButton")!;
                var popup = Assert.IsType<AppFlyout>(button.Flyout);
                OpenSummary(button, popup);
                action(window, host, row, button, popup);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void OpenSummary(AppActionButton button, AppFlyout popup)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        Activate(button);
        Dispatcher.UIThread.RunJobs();
        Assert.True(popup.IsOpen);
    }

    private static void Activate(AppActionButton button)
    {
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
