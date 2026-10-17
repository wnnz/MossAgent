using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectSummaryDrawerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ProjectSelectionKeepsDrawerOnlyWhenItOpensASummary(bool dark, bool summary) => _session.Dispatch(() =>
    {
        var selections = 0;
        var row = new AppProjectNavigationRow
        {
            Title = "project",
            ActionTarget = new object(),
            SelectCommand = new RelayCommand(() => selections++),
            SummaryContent = summary ? new AppCard { Child = new AppText { Text = "project details" } } : null
        };
        var pane = new AppNavigationPanes { NavigationPane = row, PrimaryPane = new AppInput() };
        var window = new AppWindow
        {
            Content = pane,
            Width = 526,
            Height = 601,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            pane.ToggleNavigationCommand.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(pane.IsOverlayOpen);
            var button = row.FindControl<AppActionButton>("SelectProjectButton")!;
            Assert.Equal(!summary, AppNavigationPanes.GetDismissOnClick(button));
            Assert.True(button.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(1, selections);
            Assert.Equal(summary, pane.IsOverlayOpen);
            Assert.Equal(summary, row.IsSummaryOpen);
            if (summary)
            {
                row.SummaryContent = null;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(row.IsSummaryOpen);
                Assert.True(AppNavigationPanes.GetDismissOnClick(button));
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
