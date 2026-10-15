using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectlessChatRestartTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task Restart_RestoresAndContinuesTheSameProjectlessConversation() => _session.DispatchAsync(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        await using var server = new ModelSelectionResponseServer(token);
        Guid taskId;
        try
        {
            await using (var first = AppComposition.CreateServices(root))
            {
                await first.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
                var configuration = first.GetRequiredService<IConfigurationRepository>();
                var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                    server.BaseUri, "synthetic", null, true, true, new Dictionary<string, string>());
                var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 16000, 1024,
                    "medium", null, null, false, false, true, true, "{}");
                await configuration.SaveProviderAsync(provider, token);
                await configuration.SaveModelAsync(model, token);
                var workspace = first.GetRequiredService<WorkspaceViewModel>();
                await workspace.LoadAsync();
                workspace.ComposerText = "before restart";
                Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
                workspace.PrimaryActionCommand.Execute(null);
                await WaitUntilAsync(() => server.Requests.Count == 1 && !workspace.IsRunning, token);
                taskId = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask).Id;
                await workspace.ShutdownAsync();
            }

            await using var restarted = AppComposition.CreateServices(root);
            await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            var restored = restarted.GetRequiredService<WorkspaceViewModel>();
            await restored.LoadAsync();
            await WaitUntilAsync(() => restored.Messages.Count == 2, token);
            Assert.Null(restored.Catalog.SelectedProject);
            Assert.Equal(taskId, restored.Catalog.SelectedTask?.Id);
            Assert.Equal("before restart", restored.Messages[0].Content);
            Assert.Equal(taskId, Assert.Single(restored.Catalog.RecentTasks.Items).Task.Id);
            restored.ComposerText = "after restart";
            await WaitUntilAsync(() => restored.PrimaryActionCommand.CanExecute(null), token);
            Assert.True(restored.PrimaryActionCommand.CanExecute(null));
            restored.PrimaryActionCommand.Execute(null);
            await WaitUntilAsync(() => server.Requests.Count == 2 && !restored.IsRunning, token);
            Assert.Equal(taskId, restored.Catalog.SelectedTask?.Id);
            var repository = restarted.GetRequiredService<IWorkspaceRepository>();
            Assert.Empty(await repository.GetProjectsAsync(token));
            Assert.Equal(4, (await repository.GetMessagesAsync(taskId, token)).Count);
            await restored.ShutdownAsync();
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }, TestContext.Current.CancellationToken);

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken token)
    {
        while (!predicate()) await Task.Delay(15, token);
    }
}
