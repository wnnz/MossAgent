using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceComposerDraftSeedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeedingAnAlreadyCachedEmptyScopeKeepsForegroundAndUpdatesCacheAndStorage()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        var target = new ConversationDraftScope(null, Guid.NewGuid());
        drafts.SelectContext(null, target.TaskId);
        await drafts.LoadCurrentAsync();
        drafts.SelectContext(null, null);
        await drafts.LoadCurrentAsync();
        drafts.Text = "foreground draft";
        await drafts.FlushAsync();

        Assert.True(await drafts.TrySeedAsync(target, "investigation draft", Token));

        Assert.Equal(new(null, null), drafts.Scope);
        Assert.Equal("foreground draft", drafts.Text);
        Assert.Equal("investigation draft", (await repository.GetAsync(target, Token))?.Text);
        drafts.SelectContext(null, target.TaskId);
        await drafts.LoadCurrentAsync();
        Assert.Equal("investigation draft", drafts.Text);
        Assert.False(await drafts.TrySeedAsync(target, "replacement", Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingTextOrAttachmentCannotBeReplacedEvenWhenTextIsEmpty(bool attachment)
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        if (attachment) drafts.SetAttachments(drafts.Scope,
            [new(Guid.NewGuid(), "C:\\fixture\\selected.txt", "selected.txt", "selected content", 16, "fixture-authorization")]);
        else drafts.Text = "user draft";

        Assert.False(await drafts.TrySeedAsync(drafts.Scope, "investigation draft", Token));
        await drafts.FlushAsync();

        Assert.Equal(attachment ? string.Empty : "user draft", drafts.Text);
        Assert.Equal(attachment ? 1 : 0, drafts.Attachments.Count);
        Assert.DoesNotContain("investigation", (await repository.GetAsync(drafts.Scope, Token))!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalRevisionConflictKeepsBothVersionsAndRequiresExplicitRecovery()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        Assert.True(await repository.SaveAsync([new(drafts.Scope, "external user draft", 0)], Token));

        Assert.False(await drafts.TrySeedAsync(drafts.Scope, "investigation draft", Token));

        Assert.Equal("external user draft", (await repository.GetAsync(drafts.Scope, Token))?.Text);
        Assert.Equal("investigation draft", drafts.Text);
        Assert.True(drafts.IsConflict);
        Assert.False(drafts.CanSend);
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
    }

    [Fact]
    public async Task UserInputDuringSeedSaveWinsAndTheCallerCannotClaimSuccessfulSeeding()
    {
        var repository = new TestConversationDraftRepository();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.SaveHandler = async (updates, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            repository.SaveHandler = null;
            return await repository.SaveAsync(updates, token);
        };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        var seeding = drafts.TrySeedAsync(drafts.Scope, "investigation draft", Token);
        await entered.Task.WaitAsync(Token);

        drafts.Text = "newer user input";
        release.SetResult();
        Assert.False(await seeding);
        await drafts.FlushAsync();

        Assert.Equal("newer user input", drafts.Text);
        Assert.Equal(drafts.Text, (await repository.GetAsync(drafts.Scope, Token))?.Text);
        Assert.False(drafts.HasNotice);
    }

    [Fact]
    public async Task CancelledReadCannotSeedLateOrSwitchTheForegroundScope()
    {
        var repository = new TestConversationDraftRepository();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        drafts.Text = "foreground retained";
        await drafts.FlushAsync();
        repository.ReadHandler = (_, _) => { entered.TrySetResult(); return release.Task; };
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var target = new ConversationDraftScope(null, Guid.NewGuid());
        var seeding = drafts.TrySeedAsync(target, "cancelled investigation", cancel.Token);
        await entered.Task.WaitAsync(Token);

        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => seeding);
        release.SetResult(null);
        repository.ReadHandler = null;
        drafts.SelectContext(null, target.TaskId);
        await drafts.LoadCurrentAsync();
        Assert.Empty(drafts.Text);
        Assert.Null(await repository.GetAsync(target, Token));
        drafts.SelectContext(null, null);
        Assert.Equal("foreground retained", drafts.Text);
    }

    [Fact]
    public async Task InvalidInputAndDisposedComposerCannotCreateDraftEntries()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository);
        var target = new ConversationDraftScope(null, Guid.NewGuid());
        Assert.False(await drafts.TrySeedAsync(target, " ", Token));
        Assert.False(await drafts.TrySeedAsync(target, new string('x', ConversationDraft.MaximumTextLength + 1), Token));
        drafts.Dispose();
        Assert.False(await drafts.TrySeedAsync(target, "late draft", Token));
        Assert.Equal(0, repository.ReadCalls);
        Assert.Equal(0, repository.SaveCalls);
    }
}
