using System.Text;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentDraftStateTests
{
    [Fact]
    public async Task DelayedTextReadRestoresAttachmentsWithoutReplacingNewTextAndLateScopeCannotLeak()
    {
        var project = Project();
        var attachment = Attachment(project);
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestConversationDraftRepository();
        await repository.SaveAsync([new(new(project.Id, null), "old text", 0, [attachment])], TestContext.Current.CancellationToken);
        repository.ReadHandler = (scope, _) => scope.ProjectId == project.Id ? release.Task : Task.FromResult<ConversationDraft?>(null);
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        var probe = new WorkspaceAttachmentOperationProbe();
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        using var coordinator = new WorkspaceAttachmentDraftCoordinator(manager, drafts);
        coordinator.SelectContext(project, null);
        var reading = drafts.LoadCurrentAsync();
        drafts.Text = "new text while loading";
        release.SetResult(new("old text", 1, [attachment]));
        await reading;
        Assert.Equal("new text while loading", drafts.Text);
        Assert.Equal(attachment.Id, Assert.Single(manager.Items).SelectionId);
        await drafts.FlushAsync();

        var other = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ReadHandler = (scope, _) => scope.ProjectId == project.Id ? other.Task : Task.FromResult<ConversationDraft?>(null);
        coordinator.SelectContext(project, Guid.NewGuid());
        var oldRead = drafts.LoadCurrentAsync();
        coordinator.SelectContext(null, null);
        other.SetResult(null);
        await Task.WhenAll(oldRead, drafts.LoadCurrentAsync());
        Assert.Empty(manager.Items);
        Assert.Null(drafts.Scope.ProjectId);
    }

    [Fact]
    public async Task DelayedReadCannotOverwriteExplicitNewAttachmentSelection()
    {
        var project = Project();
        var old = Attachment(project);
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestConversationDraftRepository();
        await repository.SaveAsync([new(new(project.Id, null), "stored text", 0, [old])], TestContext.Current.CancellationToken);
        repository.ReadHandler = (scope, _) => scope.ProjectId == project.Id ? release.Task : Task.FromResult<ConversationDraft?>(null);
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        var probe = new WorkspaceAttachmentOperationProbe();
        probe.SetContent(project, "new selection");
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        using var coordinator = new WorkspaceAttachmentDraftCoordinator(manager, drafts);
        coordinator.SelectContext(project, null);
        var reading = drafts.LoadCurrentAsync();
        await manager.AddFilesCommand.ExecuteAsync(project);
        var selected = Assert.Single(manager.Items);
        release.SetResult(new("stored text", 1, [old]));
        await reading;
        Assert.Same(selected, Assert.Single(manager.Items));
        Assert.NotEqual(old.Id, selected.SelectionId);
        Assert.Equal("stored text", drafts.Text);
        Assert.Contains("new selection", manager.BuildContext());
    }

    [Fact]
    public async Task UnavailableAttachmentBlocksSendingAndUnsavedCloseUntilExplicitRemoval()
    {
        var project = Project();
        var scope = new ConversationDraftScope(project.Id, null);
        var unavailable = Attachment(project) with { Content = null };
        var repository = new TestConversationDraftRepository();
        await repository.SaveAsync([new(scope, "saved text", 0, [unavailable])], TestContext.Current.CancellationToken);
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        var probe = new WorkspaceAttachmentOperationProbe();
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        using var coordinator = new WorkspaceAttachmentDraftCoordinator(manager, drafts);
        coordinator.SelectContext(project, null);
        await drafts.LoadCurrentAsync();
        var chip = Assert.Single(manager.Items);
        Assert.False(chip.IsAvailable);
        Assert.False(drafts.CanSend);
        Assert.True(drafts.HasNotice);
        Assert.Equal(WorkspaceText.Get("DraftAttachmentUnavailable"), drafts.Notice);
        drafts.Text = "new edit must survive";
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        Assert.Equal("new edit must survive", drafts.Text);
        chip.RemoveCommand.Execute(null);
        await drafts.FlushAsync();
        Assert.True(drafts.CanSend);
        Assert.False(drafts.HasNotice);
        Assert.Empty((await repository.GetAsync(scope, TestContext.Current.CancellationToken))!.AttachmentItems);
        Assert.Equal("new edit must survive", (await repository.GetAsync(scope, TestContext.Current.CancellationToken))!.Text);
    }

    [Fact]
    public async Task ChangedAuthorizationKeepsRemovableChipsButNeverAuthorizesOldSelection()
    {
        var project = Project();
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        var probe = new WorkspaceAttachmentOperationProbe();
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        using var coordinator = new WorkspaceAttachmentDraftCoordinator(manager, drafts);
        coordinator.SelectContext(project, null);
        await drafts.LoadCurrentAsync();
        await manager.AddFilesCommand.ExecuteAsync(project);
        await drafts.FlushAsync();
        var original = Assert.Single(manager.Items).SelectionId;
        var changed = project with { AuthorizedDirectories = [Path.Combine(project.PrimaryDirectory, "different-root")] };
        coordinator.SelectContext(changed, null);
        Assert.Equal(original, Assert.Single(manager.Items).SelectionId);
        Assert.False(drafts.CanSend);
        Assert.Equal(WorkspaceText.Get("DraftAttachmentUnauthorized"), drafts.Notice);
        manager.Items[0].RemoveCommand.Execute(null);
        probe.SetContent(changed, "newly authorized selection");
        await manager.AddFilesCommand.ExecuteAsync(changed);
        await drafts.FlushAsync();
        Assert.True(drafts.CanSend);
        Assert.NotEqual(original, Assert.Single(manager.Items).SelectionId);
        Assert.False(drafts.HasNotice);
    }

    [Fact]
    public async Task SubmissionConsumesIdentityNotSourcePathAndAtomicallyPromotesNextAttachments()
    {
        var project = Project();
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        drafts.SelectContext(project.Id, null);
        await drafts.LoadCurrentAsync();
        var source = drafts.Scope;
        var captured = Attachment(project);
        drafts.Text = "sent";
        drafts.SetAttachments(source, [captured]);
        await drafts.FlushAsync();
        var next = captured with { Id = Guid.NewGuid(), Content = "next", ByteCount = 4 };
        drafts.Text = "next message";
        drafts.SetAttachments(source, [next]);
        drafts.AcceptSubmission(source, Guid.NewGuid(), "sent", [captured.Id]);
        await drafts.FlushAsync();
        Assert.Equal(next, Assert.Single(drafts.Attachments));
        Assert.Empty((await repository.GetAsync(source, TestContext.Current.CancellationToken))!.AttachmentItems);
        var promoted = (await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!;
        Assert.Equal("next message", promoted.Text);
        Assert.Equal(next, Assert.Single(promoted.AttachmentItems));
    }

    private static ProjectProfile Project()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.DraftStateTests", Guid.NewGuid().ToString("N"));
        return new(Guid.NewGuid(), "draft isolation", root, [root], DateTimeOffset.UtcNow);
    }

    private static ConversationDraftAttachment Attachment(ProjectProfile project) =>
        new(Guid.NewGuid(), Path.Combine(project.PrimaryDirectory, "selected.txt"), "selected.txt", "stored content",
            Encoding.UTF8.GetByteCount("stored content"), WorkspaceAttachmentAuthorization.Create(project));
}
