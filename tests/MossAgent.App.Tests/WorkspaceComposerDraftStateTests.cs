using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceComposerDraftStateTests
{
    [Fact]
    public async Task LateReadCannotReplaceAnotherConversationsText()
    {
        var repository = new TestConversationDraftRepository();
        var scope = new ConversationDraftScope(Guid.NewGuid(), Guid.NewGuid());
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ReadHandler = (candidate, _) => candidate == scope ? release.Task : Task.FromResult<ConversationDraft?>(null);
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        drafts.SelectContext(scope.ProjectId, scope.TaskId);
        var reading = drafts.LoadCurrentAsync();
        drafts.SelectContext(null, null);
        await drafts.LoadCurrentAsync();
        drafts.Text = "当前无项目聊天";

        release.SetResult(new("旧对话内容", 4));
        await reading.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal("当前无项目聊天", drafts.Text);
        drafts.SelectContext(scope.ProjectId, scope.TaskId);
        Assert.Equal("旧对话内容", drafts.Text);
    }

    [Fact]
    public async Task TypingBeforeReadCompletesWinsWithoutLosingTheStoredRevision()
    {
        var repository = new TestConversationDraftRepository();
        var scope = new ConversationDraftScope(null, null);
        Assert.True(await repository.SaveAsync([new(scope, "旧草稿", 0)], TestContext.Current.CancellationToken));
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ReadHandler = (_, _) => release.Task;
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        var reading = drafts.LoadCurrentAsync();
        drafts.Text = "新键入内容";
        Assert.False(drafts.CanSend);

        release.SetResult(new("旧草稿", 1));
        await reading.WaitAsync(TestContext.Current.CancellationToken);
        repository.ReadHandler = null;
        await drafts.FlushAsync();

        Assert.Equal("新键入内容", drafts.Text);
        Assert.Equal(new("新键入内容", 2), await repository.GetAsync(scope, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TypingDuringSaveSchedulesAnotherVersionWithoutRevertingText()
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
        drafts.Text = "保存中的文本";
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        drafts.Text = "保存期间输入的新文本";
        release.SetResult();
        await drafts.FlushAsync();

        Assert.Equal(new("保存期间输入的新文本", 2), await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken));
        Assert.Equal("保存期间输入的新文本", drafts.Text);
        Assert.False(drafts.HasNotice);
    }

    [Fact]
    public async Task SuccessfulFirstSendPromotesOnlyTheNextDraftAndClearsTheSourceScope()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        drafts.SelectContext(Guid.NewGuid(), null);
        await drafts.LoadCurrentAsync();
        var source = drafts.Scope;
        drafts.Text = "首条消息";
        await drafts.FlushAsync();
        drafts.Text = "准备期间输入的下一条";
        var taskId = Guid.NewGuid();

        drafts.AcceptSubmission(source, taskId, "首条消息");
        await drafts.FlushAsync();

        Assert.Equal(taskId, drafts.Scope.TaskId);
        Assert.Equal("准备期间输入的下一条", drafts.Text);
        Assert.Equal(string.Empty, (await repository.GetAsync(source, TestContext.Current.CancellationToken))!.Text);
        Assert.Equal(drafts.Text, (await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!.Text);
        drafts.SelectContext(source.ProjectId, null);
        Assert.Empty(drafts.Text);
    }

    [Fact]
    public async Task OversizedDraftIsRetainedNotTruncatedAndCannotBeSentOrSilentlyDiscarded()
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        var text = new string('x', ConversationDraft.MaximumTextLength + 1);
        drafts.Text = text;

        await Assert.ThrowsAsync<InvalidOperationException>(drafts.FlushAsync);

        Assert.Same(text, drafts.Text);
        Assert.False(drafts.CanSend);
        Assert.Contains("262144", drafts.Notice);
        Assert.Equal(0, repository.SaveCalls);
        drafts.Text = "缩短后的草稿";
        await drafts.FlushAsync();
        Assert.True(drafts.CanSend);
    }
}
