using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class RepositoryStatusWorkspaceLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkspaceClose_JoinsItsExplicitRepositoryStatusOwner(bool shutdown)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var probe = new RepositoryStatusProbe { BlockReads = false };
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
            var project = await services.GetRequiredService<WorkspaceProjectService>()
                .CreateAsync("lifetime", [root], TestContext.Current.CancellationToken);
            var status = new WorkspaceRepositoryStatusViewModel(probe);
            var workspace = ActivatorUtilities.CreateInstance<WorkspaceViewModel>(services, status);
            try
            {
                await workspace.LoadAsync();
                probe.BlockReads = true;
                var refresh = status.RefreshAsync(project, null);
                var read = probe.Reads[^1];
                var stop = shutdown ? workspace.ShutdownAsync() : workspace.DisposeAsync().AsTask();
                Assert.True(read.Token.IsCancellationRequested);
                Assert.False(stop.IsCompleted);
                read.Completion.SetResult("late-result");
                await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.True(refresh.IsCompletedSuccessfully);
                Assert.NotEqual("late-result", status.BranchLabel);
            }
            finally { probe.Dispose(); await workspace.DisposeAsync(); }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
