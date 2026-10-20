using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringSafetyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0xd800)]
    [InlineData(0xdc00)]
    public async Task InvalidTextEncodingCannotProduceALossyDurableReceipt(int character)
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var text = new string((char)character, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), text, null, fixture.Token));
        Assert.Equal(run, await fixture.ReloadAsync(run));
        Assert.Empty((await fixture.InputsAsync()).Items);
    }

    [Theory]
    [InlineData("idle")]
    [InlineData("archived")]
    [InlineData("policy")]
    [InlineData("worktree")]
    public async Task UnavailableOrChangedRunScopeRefusesFurtherInput(string kind)
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var input = await fixture.AcceptAsync(run);
        run = await fixture.ReloadAsync(run);
        if (kind == "idle") await fixture.Workspaces.SaveTaskAsync(fixture.Task with { Status = AgentTaskStatus.Completed }, fixture.Token);
        if (kind == "archived") await fixture.Workspaces.SetTaskArchivedAsync(run.TaskId, true, fixture.Token);
        if (kind == "policy") await fixture.Workspaces.SaveTaskAsync(fixture.Task with { ApprovalPolicy = ApprovalPolicy.FullAccess }, fixture.Token);
        if (kind == "worktree") await fixture.Workspaces.SaveTaskAsync(fixture.Task with { WorktreePath = "changed" }, fixture.Token);
        Assert.Null(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "late", null, fixture.Token));
        Assert.Null(await fixture.Repository.ApplyAsync(run, [input.Id], null, fixture.Token));
        Assert.Equal(run, await fixture.ReloadAsync(run));
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        Assert.Equal(input, Assert.Single((await fixture.InputsAsync()).Items));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task IdleTasksCannotRegisterARun(int status)
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var task = fixture.Task with { Status = (AgentTaskStatus)status };
        await fixture.Workspaces.SaveTaskAsync(task, fixture.Token);
        Assert.Null(await fixture.Repository.OpenAsync(task, Guid.NewGuid(), fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_runs;"));
    }

    [Fact]
    public async Task StaleRunTaskAndDraftScopeAreRefusedAndProjectlessStaysProjectless()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var task = fixture.Task with { Id = Guid.NewGuid(), ProjectId = null, WorktreePath = null };
        await fixture.Workspaces.SaveTaskAsync(task, fixture.Token);
        var run = await fixture.OpenAsync(task);
        Assert.Null(await fixture.Repository.AcceptAsync(task.Id, Guid.NewGuid(), Guid.NewGuid(), "stale", null, fixture.Token));
        Assert.Null(await fixture.Repository.AcceptAsync(fixture.Task.Id, run.Id, Guid.NewGuid(), "foreign", null, fixture.Token));
        Assert.Null(await fixture.Repository.GetRunAsync(fixture.Task.Id, run.Id, fixture.Token));
        var foreign = new ConversationDraftUpdate(new(fixture.Task.ProjectId, task.Id), string.Empty, 0);
        Assert.Null(await fixture.Repository.AcceptAsync(task.Id, run.Id, Guid.NewGuid(), "foreign draft", foreign, fixture.Token));
        Assert.NotNull(await fixture.Repository.AcceptAsync(task.Id, run.Id, Guid.NewGuid(), "read only guidance",
            new(new(null, task.Id), string.Empty, 0), fixture.Token));
        var stored = Assert.Single(await fixture.Workspaces.GetTasksAsync(null, fixture.Token));
        Assert.Null(stored.ProjectId);
        Assert.Null(stored.WorktreePath);
        Assert.Equal(ApprovalPolicy.ReadOnly, stored.ApprovalPolicy);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(task.Id, fixture.Token));
    }

    [Fact]
    public async Task InvalidInputPaginationAndCancellationLeaveNoReceipt()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), " ", null, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.Empty, "text", null, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(),
            new string('a', ConversationSteeringInput.MaximumTextLength + 1), null, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.GetInputsAsync(run.TaskId, long.MaxValue, 65, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.GetInputsAsync(run.TaskId, 0, 1, fixture.Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "cancelled", null, cancellation.Token));
        Assert.Equal(run, await fixture.ReloadAsync(run));
        Assert.Empty((await fixture.InputsAsync()).Items);
    }

    [Fact]
    public async Task PendingCountAndUtf8BudgetsAreEnforcedBeforeDraftConsumption()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        for (var index = 0; index < 16; index++) await fixture.AcceptAsync(run);
        Assert.Null(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "overflow", null, fixture.Token));
        Assert.Equal(16, (await fixture.InputsAsync()).Items.Count);
        var current = await fixture.ReloadAsync(run);
        Assert.True(await fixture.Repository.CloseAsync(current, true, fixture.Token));
        run = await fixture.OpenAsync();
        await fixture.AcceptAsync(run, new string('界', 32768));
        await fixture.AcceptAsync(run, new string('界', 32768));
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, run.TaskId);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(await drafts.SaveAsync([new(scope, "retain overflow", 0)], fixture.Token));
        Assert.Null(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), new string('界', 32768), new(scope, string.Empty, 1), fixture.Token));
        Assert.Equal(new("retain overflow", 1), await drafts.GetAsync(scope, fixture.Token));
        Assert.Equal(2, (await fixture.ReloadAsync(run)).Revision);
    }
}
