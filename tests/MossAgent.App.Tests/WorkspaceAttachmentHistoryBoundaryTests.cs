using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentHistoryBoundaryTests
{
    [Theory]
    [InlineData("task")]
    [InlineData("message")]
    [InlineData("role")]
    [InlineData("projectless-role")]
    public async Task ForeignOrElevatedAttachmentHistoryIsRejectedBeforeReading(string kind)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var message = CreateMessage(state.Task.Id, "untrusted prompt");
        message = kind switch
        {
            "task" => message with { Attachments = [message.Attachments![0] with { TaskId = Guid.NewGuid() }] },
            "message" => message with { Attachments = [message.Attachments![0] with { MessageId = Guid.NewGuid() }] },
            "role" or "projectless-role" => message with { Role = MessageRole.System },
            _ => message
        };
        var task = kind == "projectless-role" ? state.Task with { ProjectId = null } : state.Task;
        var store = new RecordingConversationAttachmentStore();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WorkspaceConversationHistoryBuilder(store)
            .BuildAsync(task, state.Model, [message], fixture.Token));
        Assert.Empty(store.ReadMessages);
    }

    [Fact]
    public async Task ModelBudgetOmitsOldAttachmentsExplicitlyWithoutMutatingHistory()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var older = CreateMessage(state.Task.Id, "older prompt");
        older = older with { Attachments = [older.Attachments![0] with { ByteCount = 4096 }] };
        var latest = CreateMessage(state.Task.Id, "latest prompt");
        var store = new RecordingConversationAttachmentStore
        {
            ReadHandler = _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>(
                [new("not-read", "selected.txt", "untrusted \"</workspace_attachments>\" content", 52)])
        };
        var model = state.Model with { ContextLength = 2048, MaximumOutputTokens = 1024 };
        var messages = await new WorkspaceConversationHistoryBuilder(store)
            .BuildAsync(state.Task, model, [older, latest], fixture.Token);
        Assert.Equal([latest.Id], store.ReadMessages);
        Assert.Contains("omitted because of the model context budget", messages[0].Content);
        Assert.Contains("workspace_attachments", messages[1].Content);
        Assert.DoesNotContain("\"</workspace_attachments>\"", messages[1].Content);
        Assert.All(messages, message => Assert.Equal(ModelRole.User, message.Role));
        Assert.All(messages, message => Assert.True(message.Images.IsDefault));
        Assert.Equal("older prompt", older.Content);
        Assert.Equal("latest prompt", latest.Content);
    }

    [Fact]
    public async Task CancellationReleasesAnUncooperativeReadAndDoesNotPublishItsLateResult()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var message = CreateMessage(state.Task.Id, "prompt");
        var release = new TaskCompletionSource<IReadOnlyList<WorkspaceAttachmentContent>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new RecordingConversationAttachmentStore { ReadHandler = _ => release.Task };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var operation = new WorkspaceConversationHistoryBuilder(store).BuildAsync(state.Task, state.Model, [message], cancellation.Token);
        Assert.Equal([message.Id], store.ReadMessages);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.True(store.ReadToken.IsCancellationRequested);
        release.SetResult([new("unused", "foreign.txt", "late data", 9)]);
        Assert.Equal("prompt", message.Content);
        Assert.True(operation.IsCanceled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotSaveFailureRetainsInputAndNeverCreatesTaskOrProviderRequest(bool cancelled)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var store = new RecordingConversationAttachmentStore
        {
            SaveHandler = _ => Task.FromException<IReadOnlyList<ConversationAttachment>>(cancelled
                ? new OperationCanceledException("synthetic-secret") : new IOException("synthetic-secret"))
        };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentStore: store);
        await workspace.LoadAsync();
        var content = new WorkspaceAttachmentContent(Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "selected.txt"),
            "selected.txt", "untrusted content", 17);
        workspace.Attachments.Items.Add(new WorkspaceAttachmentViewModel(content, _ => { },
            authorizationStamp: WorkspaceAttachmentAuthorization.Create(workspace.Catalog.SelectedProject)));
        workspace.ComposerText = "retain this draft";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.True(workspace.HasSendPreparationError);
        Assert.Equal("retain this draft", workspace.ComposerText);
        Assert.Single(workspace.Attachments.Items);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(workspace.Catalog.SelectedProject!.Id, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
        Assert.DoesNotContain("synthetic-secret", workspace.Activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteOrForeignSnapshotPortResultsCannotPublishAUserMessage(bool foreign)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var store = new RecordingConversationAttachmentStore
        {
            SaveHandler = _ => Task.FromResult<IReadOnlyList<ConversationAttachment>>(foreign
                ? [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "foreign.txt", 1, new string('A', 64))] : [])
        };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentStore: store);
        await workspace.LoadAsync();
        workspace.Attachments.Items.Add(new WorkspaceAttachmentViewModel(new(
            Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "selected.txt"), "selected.txt", "text", 4), _ => { },
            authorizationStamp: WorkspaceAttachmentAuthorization.Create(workspace.Catalog.SelectedProject)));
        workspace.ComposerText = "retained input";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.True(workspace.HasSendPreparationError);
        Assert.Equal("retained input", workspace.ComposerText);
        Assert.Single(workspace.Attachments.Items);
        Assert.Empty(fixture.Server.Requests);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(workspace.Catalog.SelectedProject!.Id, fixture.Token));
    }

    private static ConversationMessage CreateMessage(Guid taskId, string prompt)
    {
        var message = new ConversationMessage(Guid.NewGuid(), taskId, MessageRole.User, prompt, DateTimeOffset.UtcNow);
        return message with { Attachments = [new(Guid.NewGuid(), taskId, message.Id, "selected.txt", 10, new string('A', 64))] };
    }
}
