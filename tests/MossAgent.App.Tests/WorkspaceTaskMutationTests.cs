using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTaskMutationTests
{
    [Theory]
    [InlineData("Rename")]
    [InlineData("Archive")]
    [InlineData("Restore")]
    [InlineData("Pin")]
    public async Task FailedWrite_PreservesStateAndCanBeRetried(string operation)
    {
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, _) => Task.FromException(new IOException("synthetic-secret-marker")),
            ArchiveHandler = (_, _, _) => Task.FromException(new IOException("synthetic-secret-marker")),
            PinHandler = (_, _, _) => Task.FromException<bool>(new IOException("synthetic-secret-marker"))
        };
        var task = CreateTask("original");
        var management = CreateManagement(repository, task);
        var published = 0;
        Subscribe(management, _ => published++);

        var exception = await Record.ExceptionAsync(() => Command(management, operation).ExecuteAsync(task));

        Assert.Null(exception);
        Assert.Equal(SettingsText.Get($"Task{operation}Failed"), management.Status);
        Assert.DoesNotContain("synthetic-secret-marker", management.Status);
        Assert.Equal(0, published);
        Assert.Same(task, management.SelectedTask);
        Assert.Same(task, management.SelectedArchivedTask);
        Assert.True(management.IsEditing);
        Assert.Equal("saved title", management.EditingTitle);
        Assert.True(Command(management, operation).CanExecute(task));

        repository.SaveTaskHandler = null;
        repository.ArchiveHandler = null;
        repository.PinHandler = null;
        await Command(management, operation).ExecuteAsync(task);

        Assert.Equal(1, published);
        Assert.Equal(SettingsText.Get($"Task{operation}Succeeded"), management.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateRename_PreservesChangedOrReopenedDraftForSameTask(bool reopen)
    {
        var completion = new TaskCompletionSource();
        var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => completion.Task };
        var task = CreateTask("original");
        var management = CreateManagement(repository, task);
        management.TaskRenamed += updated => management.SelectedTask = updated;
        var pending = management.SaveRenameCommand.ExecuteAsync(null);
        if (reopen)
        {
            management.CancelRenameCommand.Execute(null);
            management.BeginRenameCommand.Execute(task);
        }
        management.EditingTitle = "newer unsaved title";
        completion.SetResult();
        await pending;

        Assert.True(management.IsEditing);
        Assert.Equal("newer unsaved title", management.EditingTitle);
        Assert.Equal("saved title", management.SelectedTask?.Title);
        Assert.Equal("saved title", repository.SavedTask?.Title);
        Assert.Equal(SettingsText.Get("TaskPreviousDraftSaved"), management.Status);
        Assert.True(management.SaveRenameCommand.CanExecute(null));

        await management.SaveRenameCommand.ExecuteAsync(null);
        Assert.Equal("newer unsaved title", repository.SavedTask?.Title);
        Assert.False(management.IsEditing);
    }

    [Fact]
    public void SameTaskMetadataRefresh_DoesNotCancelRenameDraft()
    {
        var task = CreateTask("original");
        var management = CreateManagement(new TestWorkspaceRepository(), task);

        management.SelectedTask = task with { IsPinned = true };

        Assert.True(management.IsEditing);
        Assert.Equal("saved title", management.EditingTitle);
        management.SelectedTask = CreateTask("another conversation");
        Assert.False(management.IsEditing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PinThenRename_KeepsThePinnedStateForSelectedAndUnselectedRows(bool selected)
    {
        var repository = new TestWorkspaceRepository();
        var task = CreateTask("original");
        var management = CreateManagement(repository, task);
        if (!selected)
        {
            management.SelectedTask = CreateTask("active conversation");
            management.BeginRenameCommand.Execute(task);
            management.EditingTitle = "saved title";
        }

        await management.TogglePinCommand.ExecuteAsync(task);
        await management.SaveRenameCommand.ExecuteAsync(null);

        Assert.True(repository.SavedTask?.IsPinned);
        Assert.Equal("saved title", Assert.Single(management.PinnedTasks.Items).Task.Title);
        if (selected) Assert.True(management.SelectedTask?.IsPinned);
        else Assert.Equal("active conversation", management.SelectedTask?.Title);
    }

    [Theory]
    [InlineData("Rename")]
    [InlineData("Archive")]
    [InlineData("Restore")]
    [InlineData("Pin")]
    public async Task PendingWrite_PreventsOverlappingMutationsIncludingDirectExecution(string operation)
    {
        var completion = new TaskCompletionSource();
        var writes = 0;
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, _) => { writes++; return completion.Task; },
            ArchiveHandler = (_, _, _) => { writes++; return completion.Task; },
            PinHandler = async (_, _, _) => { writes++; await completion.Task; return true; }
        };
        var task = CreateTask("original");
        var management = CreateManagement(repository, task);
        var pending = Command(management, operation).ExecuteAsync(task);
        try
        {
            foreach (var name in new[] { "Rename", "Archive", "Restore", "Pin" })
            {
                Assert.False(Command(management, name).CanExecute(task));
                var attempted = Command(management, name).ExecuteAsync(task);
                Assert.True(attempted.IsCompletedSuccessfully);
            }
            Assert.Equal(1, writes);
        }
        finally { completion.TrySetResult(); }
        await pending;
    }

    [Theory]
    [InlineData("Rename")]
    [InlineData("Archive")]
    [InlineData("Restore")]
    [InlineData("Pin")]
    public async Task Dispose_CancelsOwnedWriteAndSuppressesLatePublication(string operation)
    {
        var completion = new TaskCompletionSource();
        var capturedToken = CancellationToken.None;
        Task Delay(CancellationToken token) { capturedToken = token; return completion.Task; }
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, token) => Delay(token),
            ArchiveHandler = (_, _, token) => Delay(token),
            PinHandler = async (_, _, token) => { await Delay(token); return true; }
        };
        var task = CreateTask("original");
        var management = CreateManagement(repository, task);
        var published = 0;
        Subscribe(management, _ => published++);
        var pending = Command(management, operation).ExecuteAsync(task);
        try
        {
            Assert.True(capturedToken.CanBeCanceled);
            Assert.IsAssignableFrom<IDisposable>(management).Dispose();
            Assert.True(capturedToken.IsCancellationRequested);
            foreach (var name in new[] { "Rename", "Archive", "Restore", "Pin" })
                Assert.False(Command(management, name).CanExecute(task));
        }
        finally { completion.TrySetResult(); }
        await pending;

        Assert.Equal(0, published);
        Assert.Same(task, management.SelectedTask);
        Assert.Empty(management.Status);
    }

    [Fact]
    public async Task LateRestore_DoesNotClearAnotherArchivedSelection()
    {
        var completion = new TaskCompletionSource();
        var repository = new TestWorkspaceRepository { ArchiveHandler = (_, _, _) => completion.Task };
        var task = CreateTask("original");
        var other = CreateTask("another archive");
        var management = CreateManagement(repository, task);
        var pending = management.RestoreCommand.ExecuteAsync(null);
        management.SelectedArchivedTask = other;
        completion.SetResult();
        await pending;

        Assert.Same(other, management.SelectedArchivedTask);
    }

    private static WorkspaceTaskManagementViewModel CreateManagement(TestWorkspaceRepository repository, AgentTask task)
    {
        var management = new WorkspaceTaskManagementViewModel(repository)
        {
            SelectedTask = task,
            SelectedArchivedTask = task
        };
        management.ArchivedTasks.Add(task);
        management.BeginRenameCommand.Execute(task);
        management.EditingTitle = "saved title";
        return management;
    }

    private static IAsyncRelayCommand Command(WorkspaceTaskManagementViewModel management, string operation) =>
        operation switch
        {
            "Rename" => management.SaveRenameCommand,
            "Archive" => management.ArchiveCommand,
            "Restore" => management.RestoreCommand,
            "Pin" => management.TogglePinCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static void Subscribe(WorkspaceTaskManagementViewModel management, Action<AgentTask> handler)
    {
        management.TaskRenamed += handler;
        management.TaskArchived += handler;
        management.TaskRestored += handler;
        management.TaskPinChanged += handler;
    }

    private static AgentTask CreateTask(string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(Guid.NewGuid(), Guid.NewGuid(), title, ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, null);
    }
}
