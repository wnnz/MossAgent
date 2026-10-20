using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentDraftRecoveryTests
{
    [Fact]
    public async Task CommittedButLostAcknowledgementReconcilesAttachmentIdentityAndText()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        drafts.SelectContext(Guid.NewGuid(), null);
        await drafts.LoadCurrentAsync();
        var attachment = Attachment();
        repository.SaveHandler = async (updates, token) =>
        {
            repository.SaveHandler = null;
            Assert.True(await repository.SaveAsync(updates, token));
            throw new IOException("synthetic lost acknowledgement");
        };
        drafts.Text = "same text";
        drafts.SetAttachments(drafts.Scope, [attachment]);
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        await drafts.RetryCommand.ExecuteAsync(null);
        await drafts.FlushAsync();
        Assert.False(drafts.HasNotice);
        Assert.Equal(attachment, Assert.Single((await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!.AttachmentItems));
    }

    [Fact]
    public async Task SameTextAndRevisionDoNotConcealCompetingAttachmentChanges()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        drafts.SelectContext(Guid.NewGuid(), null);
        await drafts.LoadCurrentAsync();
        var local = Attachment();
        var external = Attachment();
        repository.SaveHandler = async (updates, token) =>
        {
            repository.SaveHandler = null;
            Assert.True(await repository.SaveAsync(updates.Select(update => update with { Attachments = [external] }).ToArray(), token));
            return false;
        };
        drafts.Text = "identical text";
        drafts.SetAttachments(drafts.Scope, [local]);
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        await drafts.RetryCommand.ExecuteAsync(null);
        Assert.True(drafts.IsConflict);
        Assert.False(drafts.CanSend);
        Assert.Equal(local, Assert.Single(drafts.Attachments));
        Assert.Equal(external, Assert.Single((await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!.AttachmentItems));
        await drafts.KeepLocalCommand.ExecuteAsync(null);
        await drafts.FlushAsync();
        Assert.False(drafts.IsConflict);
        Assert.Equal(local, Assert.Single((await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!.AttachmentItems));
    }

    private static ConversationDraftAttachment Attachment() => new(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "selected.txt"),
        "selected.txt", "content", 7, new string('A', 64));
}
