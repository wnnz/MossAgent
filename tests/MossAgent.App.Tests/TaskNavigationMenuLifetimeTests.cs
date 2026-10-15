using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TaskNavigationMenuLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "hidden")]
    [InlineData(true, "hidden")]
    [InlineData(false, "disabled")]
    [InlineData(true, "disabled")]
    [InlineData(false, "parent-hidden")]
    [InlineData(true, "parent-hidden")]
    [InlineData(false, "parent-disabled")]
    [InlineData(true, "parent-disabled")]
    [InlineData(false, "context")]
    [InlineData(true, "context")]
    [InlineData(false, "target")]
    [InlineData(true, "target")]
    [InlineData(false, "command")]
    [InlineData(true, "command")]
    [InlineData(false, "detached")]
    [InlineData(true, "detached")]
    public Task UnavailableOwner_ClosesOpenMenuWithoutInvokingAnAction(bool dark, string change) =>
        WithRow(dark, (window, parent, row, flyout) =>
        {
            var invoked = 0;
            row.TogglePinCommand = new RelayCommand<object?>(_ => invoked++);
            flyout.ShowAt(row.FindControl<AppIconButton>("TaskOptionsButton")!);
            window.UpdateLayout();
            Assert.True(flyout.IsOpen);
            switch (change)
            {
                case "hidden": row.IsVisible = false; break;
                case "disabled": row.IsEnabled = false; break;
                case "parent-hidden": parent.IsVisible = false; break;
                case "parent-disabled": parent.IsEnabled = false; break;
                case "context": row.DataContext = new object(); break;
                case "target": row.CommandParameter = new object(); break;
                case "command": row.TogglePinCommand = new RelayCommand<object?>(_ => invoked++); break;
                case "detached": parent.Children.Remove(row); break;
            }
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
            Assert.False(row.IsOptionsOpen);
            Assert.Equal(0, invoked);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SelectionAndTitleUpdates_KeepTheCurrentMenuAndCommand(bool dark) =>
        WithRow(dark, (window, parent, row, flyout) =>
        {
            var command = row.TogglePinCommand;
            flyout.ShowAt(row.FindControl<AppIconButton>("TaskOptionsButton")!);
            window.UpdateLayout();
            Assert.True(flyout.IsOpen);
            row.Title = "Updated title";
            row.IsSelected = false;
            Assert.True(flyout.IsOpen);
            Assert.Same(command, row.TogglePinCommand);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReattachedRow_TracksOnlyItsCurrentAncestors(bool dark) =>
        WithRow(dark, (window, parent, row, flyout) =>
        {
            parent.Children.Remove(row);
            var newParent = new StackPanel { Children = { row } };
            window.Content = newParent;
            flyout.ShowAt(row.FindControl<AppIconButton>("TaskOptionsButton")!);
            window.UpdateLayout();
            Assert.True(flyout.IsOpen);
            parent.IsVisible = false;
            parent.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(flyout.IsOpen);
            newParent.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
            Assert.False(row.IsOptionsOpen);
        });

    private Task WithRow(bool dark, Action<AppWindow, StackPanel, AppTaskNavigationRow, AppFlyout> test) =>
        _session.Dispatch(() =>
        {
            var row = new AppTaskNavigationRow
            {
                Title = "Task",
                IsSelected = true,
                CommandParameter = new object(),
                TogglePinCommand = new RelayCommand<object?>(_ => { })
            };
            var parent = new StackPanel { Children = { row } };
            var window = new AppWindow
            {
                Width = 264,
                Height = 200,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = parent
            };
            window.Show();
            try
            {
                var options = row.FindControl<AppIconButton>("TaskOptionsButton")!;
                test(window, parent, row, Assert.IsType<AppFlyout>(options.Flyout));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
