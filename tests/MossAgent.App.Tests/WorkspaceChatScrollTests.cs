using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceChatScrollTests
{
    [Fact]
    public void ScrollFollow_TracksBottomUntilUserScrollsAway()
    {
        var follow = new ChatScrollFollowState();

        Assert.True(follow.Update(100, 0, 0, 100));
        Assert.False(follow.Update(0, 0, -40, 100));
        Assert.False(follow.Update(60, 0, 0, 160));
        Assert.False(follow.IsFollowing);

        follow.Update(0, 0, 40, 12);
        Assert.True(follow.IsFollowing);
        Assert.True(follow.Update(40, 0, 0, 52));

        follow.Update(0, 0, -40, 90);
        follow.Reset();
        Assert.True(follow.IsFollowing);
    }

    [Fact]
    public void ActiveMessageRefresh_DoesNotRebuildUnchangedStreamingMessages()
    {
        var repository = new TestWorkspaceRepository();
        var catalog = new WorkspaceCatalogViewModel(repository,
            null,
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
        var task = new AgentTask(Guid.NewGuid(), Guid.NewGuid(), "streaming",
            ApprovalPolicy.AskEveryTime, AgentTaskStatus.Running,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var message = new ChatMessageViewModel("MossAgent", "first");
        var active = new List<ChatMessageViewModel> { message };
        catalog.ActiveMessagesProvider = _ => active;
        catalog.SelectedTask = task;
        Assert.Same(message, Assert.Single(catalog.Messages));

        var changes = 0;
        catalog.Messages.CollectionChanged += (_, _) => changes++;
        message.Append(" delta");
        catalog.ShowActiveMessages(task.Id);
        Assert.Equal(0, changes);

        var next = new ChatMessageViewModel("MossAgent", "second");
        active.Add(next);
        catalog.ShowActiveMessages(task.Id);
        Assert.Same(next, catalog.Messages[1]);
        Assert.True(changes > 0);
    }
}
