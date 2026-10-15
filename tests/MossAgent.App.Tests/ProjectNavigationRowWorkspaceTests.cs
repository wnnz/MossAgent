using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectNavigationRowWorkspaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualSidebar_UsesProjectCommandsAndRetainsSummaryEditing(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = 980,
            Height = 640,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var catalog = fixture.Workspace.Catalog;
            var navigation = Assert.Single(catalog.ProjectNavigation);
            var row = Assert.Single(window.GetVisualDescendants().OfType<AppProjectNavigationRow>());
            Assert.Same(navigation, row.DataContext);
            Assert.Same(catalog.SelectProjectCommand, row.SelectCommand);
            Assert.Same(navigation.ToggleExpandedCommand, row.ToggleExpandedCommand);
            var toggle = row.FindControl<AppIconButton>("ToggleProjectButton")!;
            Assert.True(navigation.IsExpanded);
            Activate(toggle);
            Assert.False(navigation.IsExpanded);
            Assert.False(row.IsExpanded);
            Activate(toggle);
            Assert.True(navigation.IsExpanded);
            var select = row.FindControl<AppActionButton>("SelectProjectButton")!;
            Activate(select);
            window.UpdateLayout();
            Assert.True(navigation.IsSummaryOpen);
            var edit = Assert.Single(row.SummaryContent!.GetVisualDescendants().OfType<AppActionButton>(), button => button.Label == "编辑项目");
            Assert.Same(catalog.EditProjectCommand, edit.Command);
            Assert.Same(navigation, edit.CommandParameter);
            Activate(edit);
            Assert.True(catalog.ProjectEditor.IsOpen);
            Assert.Equal(navigation.Project.Name, catalog.ProjectEditor.Name);
            Assert.False(navigation.IsSummaryOpen);
            Assert.Empty(fixture.Server.Requests);
            catalog.ProjectEditor.IsOpen = false;
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        Dispatcher.UIThread.RunJobs();
    }
}
