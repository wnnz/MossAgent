using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceComposerDraftRecoveryTests
{
    [Fact]
    public async Task ReadFailurePreservesInputAndRetryActuallyReadsAgain()
    {
        var repository = new TestConversationDraftRepository
        {
            ReadHandler = (_, _) => Task.FromException<ConversationDraft?>(new IOException("synthetic-secret"))
        };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        drafts.Text = "保留本窗口输入";
        await drafts.LoadCurrentAsync();
        Assert.False(drafts.CanSend);
        Assert.True(drafts.HasNotice);
        Assert.DoesNotContain("synthetic-secret", drafts.Notice);
        repository.ReadHandler = null;

        await drafts.RetryCommand.ExecuteAsync(null);
        await drafts.FlushAsync();

        Assert.True(drafts.CanSend);
        Assert.True(repository.ReadCalls >= 2);
        Assert.Equal("保留本窗口输入", (await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!.Text);
    }

    [Fact]
    public async Task FailedSaveBlocksShutdownAndAnExplicitRetryOnlyPersistsTheDraft()
    {
        var repository = new TestConversationDraftRepository
        {
            SaveHandler = (_, _) => Task.FromException<bool>(new IOException("synthetic-secret"))
        };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        drafts.Text = "不能在关闭时丢弃";
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(drafts.StopAsync);
        Assert.DoesNotContain("synthetic-secret", failure.Message + drafts.Notice);
        Assert.Equal("不能在关闭时丢弃", drafts.Text);
        repository.SaveHandler = null;

        await drafts.StopAsync();

        Assert.Equal(drafts.Text, (await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!.Text);
        Assert.False(drafts.CanSend);
    }

    [Fact]
    public async Task SaveThatCommittedBeforeReportingFailureIsReconciledWithoutRepeatingTheWrite()
    {
        var repository = new TestConversationDraftRepository();
        repository.SaveHandler = async (updates, token) =>
        {
            repository.SaveHandler = null;
            Assert.True(await repository.SaveAsync(updates, token));
            throw new IOException("synthetic failure after commit");
        };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        drafts.Text = "已提交但未收到确认";
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        var saves = repository.SaveCalls;

        await drafts.RetryCommand.ExecuteAsync(null);

        Assert.Equal(saves, repository.SaveCalls);
        Assert.False(drafts.HasNotice);
        Assert.Equal(new(drafts.Text, 1), await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConflictingWindowCannotOverwriteUntilTheUserExplicitlyChoosesItsDraft()
    {
        var repository = new TestConversationDraftRepository();
        using var first = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        using var second = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await Task.WhenAll(first.LoadCurrentAsync(), second.LoadCurrentAsync());
        first.Text = "第一个窗口保存的内容";
        await first.FlushAsync();
        second.Text = "第二个窗口尚未保存的内容";
        await Assert.ThrowsAsync<InvalidOperationException>(second.FlushAsync);
        await second.RetryCommand.ExecuteAsync(null);
        Assert.True(second.IsConflict);
        Assert.False(second.CanSend);
        Assert.Equal(first.Text, (await repository.GetAsync(first.Scope, TestContext.Current.CancellationToken))!.Text);

        await second.KeepLocalCommand.ExecuteAsync(null);
        await second.FlushAsync();

        Assert.False(second.IsConflict);
        Assert.Equal(second.Text, (await repository.GetAsync(second.Scope, TestContext.Current.CancellationToken))!.Text);
    }

    [Fact]
    public async Task IgnoredReadCancellationTimesOutAndLateCompletionCannotPublishAfterDisposal()
    {
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken readToken = default;
        var repository = new TestConversationDraftRepository
        {
            ReadHandler = (_, token) => { readToken = token; return release.Task; }
        };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, TimeSpan.FromMilliseconds(80), TimeSpan.Zero);
        drafts.Text = "保留超时前输入";
        await drafts.LoadCurrentAsync().WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(readToken.IsCancellationRequested);
        Assert.False(drafts.CanSend);
        var notices = 0;
        drafts.PropertyChanged += (_, _) => notices++;
        drafts.Dispose();
        release.SetResult(new("不应发布的旧内容", 99));
        Assert.Equal("保留超时前输入", drafts.Text);
        Assert.Equal(0, notices);
        Assert.False(drafts.RetryCommand.CanExecute(null));
    }
}
