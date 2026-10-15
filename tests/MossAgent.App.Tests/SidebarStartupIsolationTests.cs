using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Application.Settings;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarStartupIsolationTests
{
    [Fact]
    public async Task SlowSidebarRead_DoesNotBlockWorkspaceCatalogOrModelLoading()
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-sidebar-startup-").FullName;
        var release = new TaskCompletionSource<SidebarPreference>(TaskCreationOptions.RunContinuationsAsynchronously);
        WorkspaceViewModel? workspace = null;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var token = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            var project = new ProjectProfile(Guid.NewGuid(), "startup", root, [root], DateTimeOffset.UtcNow);
            await services.GetRequiredService<IWorkspaceRepository>().SaveProjectAsync(project, token);
            var store = new TestSidebarPreferenceStore { LoadHandler = _ => release.Task };
            workspace = ActivatorUtilities.CreateInstance<WorkspaceViewModel>(services, store);
            var loadingPreferences = workspace.SidebarProjects.LoadAsync();
            await workspace.LoadAsync().WaitAsync(TimeSpan.FromSeconds(3), token);
            Assert.Equal(project.Id, workspace.Catalog.SelectedProject?.Id);
            Assert.True(workspace.SidebarProjects.IsBusy);
            Assert.False(loadingPreferences.IsCompleted);
            Assert.Equal(1, store.Loads);
            release.SetResult(new SidebarPreference(SidebarProjectSortOrder.UpdatedAt, []));
            await loadingPreferences;
            Assert.False(workspace.SidebarProjects.IsBusy);
            Assert.Single(workspace.SidebarProjects.Items);
        }
        finally
        {
            workspace?.Dispose();
            release.TrySetResult(new SidebarPreference(SidebarProjectSortOrder.UpdatedAt, []));
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
