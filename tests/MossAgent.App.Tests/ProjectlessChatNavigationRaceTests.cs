using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectlessChatNavigationRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateChatHistory_DoesNotReplaceANewerConversationOrEmptyDraft(bool send)
    {
        var repository = new TestWorkspaceRepository();
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", "D:\\fixture", ["D:\\fixture"], now);
        var old = new AgentTask(Guid.NewGuid(), null, "old chat", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        repository.ProjectItems = [project];
        repository.ProjectlessTasks = [old];
        var catalog = new WorkspaceCatalogViewModel(repository,
            null,
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
        await catalog.ReloadAsync();
        var response = new TaskCompletionSource<IReadOnlyList<AgentTask>>();
        repository.GetProjectlessTasksHandler = _ => response.Task;
        catalog.NewChatCommand.Execute(null);
        var current = old with { Id = Guid.NewGuid(), Title = "new chat", Status = AgentTaskStatus.Running };
        if (send)
        {
            catalog.UpsertTask(current);
            catalog.Messages.Add(new("你", "new message"));
        }
        else catalog.NewTaskCommand.Execute(null);
        response.SetResult([old]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (catalog.Tasks.Count != (send ? 2 : 1)) await Task.Delay(10, timeout.Token);

        Assert.Null(catalog.SelectedProject);
        Assert.Equal(send ? current.Id : (Guid?)null, catalog.SelectedTask?.Id);
        if (send)
        {
            Assert.Equal("new message", Assert.Single(catalog.Messages).Content);
            Assert.Equal(current, catalog.FindTask(null, current.Id));
        }
        else Assert.Empty(catalog.Messages);
        catalog.TaskManagement.Dispose();
        catalog.ProjectEditor.Dispose();
    }
}
