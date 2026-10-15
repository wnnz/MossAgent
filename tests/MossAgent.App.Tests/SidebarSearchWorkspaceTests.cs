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
public sealed class SidebarSearchWorkspaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualSidebar_SearchesTheCatalogWithoutChangingSelectionOrSendingTheDraft(bool dark) => _session.DispatchAsync(async () =>
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
            var workspace = fixture.Workspace;
            var search = Assert.Single(window.GetVisualDescendants().OfType<AppSidebarSearch>());
            var project = workspace.Catalog.SelectedProject;
            var task = workspace.Catalog.SelectedTask;
            var draft = workspace.ComposerText;
            Assert.Same(workspace.SidebarSearch.ClearCommand, search.ClearCommand);
            Assert.True(search.OpenSearch());
            Dispatcher.UIThread.RunJobs();
            var input = search.FindControl<AppInput>("SearchInput")!;
            Assert.True(input.IsFocused);
            input.Text = "does-not-match-any-fixture-project";
            Assert.Equal(input.Text, workspace.Catalog.TaskSearch.Query);
            Assert.False(workspace.SidebarSearch.HasMatches);
            Assert.True(search.ShowEmptyState);
            Assert.All(workspace.Catalog.ProjectNavigation, group => Assert.False(group.IsVisible));
            workspace.SidebarSearch.Query = project!.Name;
            Assert.Equal(project.Name, input.Text);
            Assert.True(workspace.SidebarSearch.HasMatches);
            Assert.False(search.ShowEmptyState);
            workspace.SidebarSearch.ClearCommand.Execute(null);
            Assert.Equal(string.Empty, input.Text);
            Assert.False(search.HasQuery);
            Assert.All(workspace.Catalog.ProjectNavigation, group => Assert.True(group.IsVisible));
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Dispatcher.UIThread.RunJobs();
            Assert.False(search.IsSearchOpen);
            Assert.Same(project, workspace.Catalog.SelectedProject);
            Assert.Same(task, workspace.Catalog.SelectedTask);
            Assert.Equal(draft, workspace.ComposerText);
            Assert.False(workspace.IsRunning);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
