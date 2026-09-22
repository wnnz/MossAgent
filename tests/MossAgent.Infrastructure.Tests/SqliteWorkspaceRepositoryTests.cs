using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteWorkspaceRepositoryTests
{
    [Fact]
    public async Task TaskAndMessages_RoundTripAndRunningTaskBecomesInterrupted()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            var initializer = new SqliteDatabaseInitializer(paths, connections);
            await initializer.InitializeAsync(cancellationToken);
            var repository = new SqliteWorkspaceRepository(connections);
            var project = CreateProject(root);
            var task = CreateTask(project.Id);
            await repository.SaveProjectAsync(project, cancellationToken);
            await repository.SaveTaskAsync(task, cancellationToken);
            await SaveMessagesAsync(repository, task.Id, cancellationToken);

            var messages = await repository.GetMessagesAsync(task.Id, cancellationToken);
            await initializer.InitializeAsync(cancellationToken);
            var restored = Assert.Single(await repository.GetTasksAsync(project.Id, cancellationToken));

            Assert.Equal(["first", "second"], messages.Select(message => message.Content));
            Assert.Equal(AgentTaskStatus.Interrupted, restored.Status);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RenameArchiveAndRestore_PreserveTaskHistory()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections)
                .InitializeAsync(cancellationToken);
            var repository = new SqliteWorkspaceRepository(connections);
            var project = CreateProject(root);
            var task = CreateTask(project.Id);
            await repository.SaveProjectAsync(project, cancellationToken);
            await repository.SaveTaskAsync(task, cancellationToken);
            await repository.AppendMessageAsync(
                new ConversationMessage(
                    Guid.NewGuid(), task.Id, MessageRole.User,
                    "preserved", DateTimeOffset.UtcNow),
                cancellationToken);

            await repository.SaveTaskAsync(task with { Title = "renamed" }, cancellationToken);
            await repository.SetTaskArchivedAsync(task.Id, true, cancellationToken);

            Assert.Empty(await repository.GetTasksAsync(project.Id, cancellationToken));
            var archived = Assert.Single(
                await repository.GetArchivedTasksAsync(project.Id, cancellationToken));
            Assert.Equal("renamed", archived.Title);
            Assert.Single(await repository.GetMessagesAsync(task.Id, cancellationToken));

            await repository.SetTaskArchivedAsync(task.Id, false, cancellationToken);

            Assert.Empty(await repository.GetArchivedTasksAsync(project.Id, cancellationToken));
            Assert.Single(await repository.GetTasksAsync(project.Id, cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task SaveMessagesAsync(
        SqliteWorkspaceRepository repository,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await repository.AppendMessageAsync(
            new ConversationMessage(Guid.NewGuid(), taskId, MessageRole.User, "first", now),
            cancellationToken);
        await repository.AppendMessageAsync(
            new ConversationMessage(
                Guid.NewGuid(), taskId, MessageRole.Assistant, "second", now.AddSeconds(1)),
            cancellationToken);
    }

    private static ProjectProfile CreateProject(string root) =>
        new(Guid.NewGuid(), "project", root, [root], DateTimeOffset.UtcNow);

    private static AgentTask CreateTask(Guid projectId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(
            Guid.NewGuid(), projectId, "task", ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Running, now, now, null);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
