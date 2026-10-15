using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectTaskPreviewWorkspaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualSidebarExpandsWithKeyboardAndKeepsSelectionCommands(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var navigation = Assert.Single(fixture.Workspace.Catalog.ProjectNavigation);
        var original = new AgentTask(Guid.NewGuid(), navigation.Project.Id, "preview", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
        navigation.SetTasks(Enumerable.Range(0, 8).Select(index => original with
        {
            Id = index == 0 ? original.Id : Guid.NewGuid(),
            Title = $"preview {index}",
            Status = AgentTaskStatus.Completed
        }));
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = 980,
            Height = 640,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { VerifyKeyboardExpansion(window, fixture, navigation); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void VerifyKeyboardExpansion(MainWindow window, WorkspaceModelSelectionFixture fixture,
        WorkspaceProjectNavigationViewModel navigation)
    {
        window.UpdateLayout();
        var button = Assert.Single(window.GetVisualDescendants().OfType<AppButton>(), control =>
            control.Classes.Contains("sidebar-project-more"));
        Assert.True(button.IsEffectivelyVisible);
        Assert.Equal(30, button.Bounds.Height);
        Assert.Equal(new Thickness(32, 0, 8, 0), button.Padding);
        Assert.Equal(default, button.Margin);
        Assert.Equal(14, button.FontSize);
        Assert.Equal(5, ProjectRows(window).Length);
        Activate(button);
        window.UpdateLayout();
        Assert.Equal(8, ProjectRows(window).Length);
        Assert.Equal("收起显示", button.Content);
        Assert.Same(fixture.Workspace.Catalog.SelectTaskCommand, ProjectRows(window)[7].SelectCommand);
        Activate(button);
        window.UpdateLayout();
        Assert.Equal(5, ProjectRows(window).Length);
        Assert.True(button.IsKeyboardFocusWithin);
        navigation.IsExpanded = false;
        window.UpdateLayout();
        Assert.False(button.IsEffectivelyVisible);
        navigation.IsExpanded = true;
        navigation.ApplyFilter("preview");
        window.UpdateLayout();
        Assert.Equal(8, ProjectRows(window).Length);
        Assert.False(button.IsEffectivelyVisible);
        Assert.Empty(fixture.Server.Requests);
    }

    private static AppTaskNavigationRow[] ProjectRows(MainWindow window) => window.GetVisualDescendants()
        .OfType<AppTaskNavigationRow>().Where(row => row.IsProjectTask && row.IsEffectivelyVisible).ToArray();

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        Dispatcher.UIThread.RunJobs();
    }
}
