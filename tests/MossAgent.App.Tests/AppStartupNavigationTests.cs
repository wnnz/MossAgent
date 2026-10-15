using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Persistence;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppStartupNavigationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingDiscovery_KeepsSettingsNavigationAndDraftAvailable(bool hasWarning)
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-startup-navigation", Guid.NewGuid().ToString("N"));
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(TestContext.Current.CancellationToken);
            var shell = services.GetRequiredService<MainWindowViewModel>();
            var state = services.GetRequiredService<AppStartupViewModel>();
            var manager = new DeferredStartupMcpManager();
            if (hasWarning) manager.Errors = new Dictionary<string, string> { ["fixture"] = "safe fixture error" };
            var queue = new ConcurrentQueue<Action>();
            await using var startup = new AppStartupService(manager, state, queue.Enqueue);
            shell.Workspace.ComposerText = "保持草稿，不发送任务";

            var loading = startup.StartAsync();
            await manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Same(state, shell.Workspace.Startup);
            Assert.True(state.IsLoading);
            Assert.False(shell.Workspace.PrimaryActionCommand.CanExecute(null));
            Assert.True(shell.ToggleSettingsCommand.CanExecute(null));
            shell.ToggleSettingsCommand.Execute(null);
            Assert.True(shell.IsSettingsOpen);
            shell.SelectSettingsTabCommand.Execute("3");
            Assert.True(shell.IsMcpSettingsSelected);
            shell.Mcp.OpenCreateFormCommand.Execute(null);
            Assert.True(shell.Mcp.IsFormOpen);
            Assert.False(loading.IsCompleted);

            manager.Release.TrySetResult();
            await loading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Single(queue)();

            Assert.True(state.IsReady);
            Assert.Equal(hasWarning, state.HasWarning);
            Assert.True(shell.IsSettingsOpen);
            Assert.True(shell.IsMcpSettingsSelected);
            Assert.True(shell.Mcp.IsFormOpen);
            Assert.Equal("保持草稿，不发送任务", shell.Workspace.ComposerText);
            shell.Mcp.CloseFormCommand.Execute(null);
            shell.CloseSettingsCommand.Execute(null);
            Assert.False(shell.IsSettingsOpen);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
