using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceMessageVisibilityTests
{
    [Theory]
    [InlineData(AgentTaskStatus.Completed, false)]
    [InlineData(AgentTaskStatus.Failed, true)]
    public async Task ReselectingPersistedTaskAfterNewConversationShowsMessages(
        AgentTaskStatus status, bool expectedFailure)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectProfile(Guid.NewGuid(), "History", root, [root], now);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "Earlier task",
                ApprovalPolicy.AskEveryTime, status, now, now, null);
            await repository.SaveProjectAsync(project, cancellationToken);
            await repository.SaveTaskAsync(task, cancellationToken);
            await repository.AppendMessageAsync(
                new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, "Earlier prompt", now),
                cancellationToken);

            var workspace = services.GetRequiredService<WorkspaceViewModel>();
            await workspace.LoadAsync();
            await WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Id == task.Id
                && workspace.Messages.Count == 1, cancellationToken);
            Assert.Equal(expectedFailure, workspace.IsSelectedTaskFailed);
            Assert.False(string.IsNullOrWhiteSpace(workspace.TaskFailureHint));

            workspace.Catalog.StartNewTask();
            Assert.True(workspace.IsEmptySession);
            Assert.False(workspace.HasMessages);
            Assert.False(workspace.IsSelectedTaskFailed);

            var notifications = new ConcurrentQueue<(string Name, bool Value)>();
            workspace.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(WorkspaceViewModel.HasMessages))
                    notifications.Enqueue((args.PropertyName, workspace.HasMessages));
                if (args.PropertyName == nameof(WorkspaceViewModel.IsEmptySession))
                    notifications.Enqueue((args.PropertyName, workspace.IsEmptySession));
            };
            workspace.Catalog.SelectTaskCommand.Execute(task);
            await WaitUntilAsync(() => workspace.Messages.Count == 1, cancellationToken);

            Assert.Equal("Earlier prompt", workspace.Messages[0].Content);
            Assert.Equal(expectedFailure, workspace.IsSelectedTaskFailed);
            Assert.Contains((nameof(WorkspaceViewModel.HasMessages), true), notifications);
            Assert.Contains((nameof(WorkspaceViewModel.IsEmptySession), false), notifications);
        }
        finally
        {
            await DeleteWhenUnlockedAsync(root);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 500 && !condition(); attempt++)
            await Task.Delay(10, cancellationToken);
        Assert.True(condition(), "Timed out waiting for task messages.");
    }

    private static async Task DeleteWhenUnlockedAsync(string root)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            SqliteTestPool.Clear(root);
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                await Task.Delay(50);
            }
        }
    }
}
