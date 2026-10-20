using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceComposerDraftRaceTests
{
    [Fact]
    public async Task PromotionDuringAnOlderSaveRetainsTheNextTextAndCommitsBothScopesTogether()
    {
        var repository = new TestConversationDraftRepository();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var batches = new List<IReadOnlyList<ConversationDraftUpdate>>();
        repository.SaveHandler = async (updates, token) =>
        {
            batches.Add(updates);
            if (batches.Count == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }
            var handler = repository.SaveHandler;
            repository.SaveHandler = null;
            try { return await repository.SaveAsync(updates, token); }
            finally { repository.SaveHandler = handler; }
        };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        var source = drafts.Scope;
        drafts.Text = "正在保存的首条消息";
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        drafts.Text = "准备中的下一条";
        drafts.AcceptSubmission(source, Guid.NewGuid(), "正在保存的首条消息");
        release.SetResult();
        await drafts.FlushAsync();

        Assert.Equal("准备中的下一条", drafts.Text);
        var last = batches.Last();
        Assert.Contains(last, update => update.Scope == source && update.Text == string.Empty && update.ExpectedRevision == 1);
        Assert.Contains(last, update => update.Scope == drafts.Scope && update.Text == "准备中的下一条");
        Assert.Equal(string.Empty, (await repository.GetAsync(source, TestContext.Current.CancellationToken))!.Text);
    }

    [Fact]
    public async Task CompletingAnOlderSubmissionCannotReplaceTheNewlySelectedDraft()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        var source = drafts.Scope;
        drafts.Text = "之前准备发送的消息";
        drafts.SelectContext(Guid.NewGuid(), null);
        await drafts.LoadCurrentAsync();
        var current = drafts.Scope;
        drafts.Text = "新选中项目的草稿";

        drafts.AcceptSubmission(source, Guid.NewGuid(), "之前准备发送的消息");
        await drafts.FlushAsync();

        Assert.Equal(current, drafts.Scope);
        Assert.Equal("新选中项目的草稿", drafts.Text);
    }

    [Fact]
    public async Task IgnoredSaveCancellationIsBoundedAndRetryDoesNotTrustItsLateResult()
    {
        var repository = new TestConversationDraftRepository();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken writeToken = default;
        repository.SaveHandler = (_, token) => { writeToken = token; return release.Task; };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, TimeSpan.FromMilliseconds(80), TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        drafts.Text = "超时保存前的内容";
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        Assert.True(writeToken.IsCancellationRequested);
        drafts.Text = "重试前的新内容";
        repository.SaveHandler = null;

        await drafts.RetryCommand.ExecuteAsync(null);
        release.SetResult(true);
        await drafts.FlushAsync();

        Assert.Equal("重试前的新内容", drafts.Text);
        Assert.Equal(new(drafts.Text, 1), await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken));
        Assert.False(drafts.HasNotice);
    }

    [Fact]
    public async Task RecoveryCommandsShareOneOperationAndCannotPublishCompetingRevisions()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        Assert.True(await repository.SaveAsync([new(drafts.Scope, "external", 0)], TestContext.Current.CancellationToken));
        drafts.Text = "local";
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ReadHandler = (_, _) => { entered.TrySetResult(); return release.Task; };
        var reads = repository.ReadCalls;
        var retry = drafts.RetryCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(drafts.RetryCommand.CanExecute(null));
        Assert.False(drafts.KeepLocalCommand.CanExecute(null));
        var competing = drafts.KeepLocalCommand.ExecuteAsync(null);
        release.SetResult(new("external", 1));

        await Task.WhenAll(retry, competing);

        Assert.Equal(reads + 1, repository.ReadCalls);
        Assert.True(drafts.IsConflict);
        Assert.True(drafts.KeepLocalCommand.CanExecute(null));
        Assert.Equal("local", drafts.Text);
    }

    [Fact]
    public async Task DisposalDuringMultiScopeRecoveryCannotStartAnotherReadOrPublishLateState()
    {
        var repository = new TestConversationDraftRepository { SaveHandler = (_, _) => Task.FromResult(false) };
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        drafts.Text = "first";
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        drafts.SelectContext(Guid.NewGuid(), null);
        await drafts.LoadCurrentAsync();
        drafts.Text = "second";
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ReadHandler = (_, _) => { entered.TrySetResult(); return release.Task; };
        var recovery = drafts.RetryCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var reads = repository.ReadCalls;

        drafts.Dispose();
        release.SetResult(new("late", 10));
        await recovery.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(reads, repository.ReadCalls);
        Assert.Equal("second", drafts.Text);
        Assert.False(drafts.CanSend);
    }

    [Fact]
    public async Task RapidScopeCreationAndBackgroundSavesKeepIndependentSnapshots()
    {
        var operations = Enumerable.Range(0, 64).Select(async index =>
        {
            var repository = new TestConversationDraftRepository();
            using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
            await drafts.LoadCurrentAsync();
            var source = drafts.Scope;
            var sent = $"sent-{index}";
            drafts.Text = sent;
            await Task.Yield();
            drafts.SelectContext(Guid.NewGuid(), null);
            var reading = drafts.LoadCurrentAsync();
            var current = drafts.Scope;
            drafts.Text = $"retained-{index}";
            drafts.AcceptSubmission(source, Guid.NewGuid(), sent);
            await reading;
            await drafts.FlushAsync();
            Assert.Equal(current, drafts.Scope);
            Assert.Equal($"retained-{index}", drafts.Text);
            Assert.Equal(drafts.Text, (await repository.GetAsync(current, TestContext.Current.CancellationToken))!.Text);
        });

        await Task.WhenAll(operations).WaitAsync(TestContext.Current.CancellationToken);
    }
}
