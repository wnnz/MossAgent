using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteTaskSkillActivationStoreTests
{
    private static TaskSkillActivation Row(Guid taskId, string name, string path, string? text = null) =>
        new(Guid.NewGuid(), taskId, Guid.NewGuid(), name, "1.0", new string('a', 64), path,
            text ?? $"body of {path}", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task SaveAndListRoundTripsRowsInNameThenPathOrder()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteTaskSkillActivationStore(fixture.Connections);
        var beta = Row(fixture.Task.Id, "beta", "skills/a/SKILL.md");
        var alphaB = Row(fixture.Task.Id, "alpha", "skills/b/SKILL.md");
        var alphaA = Row(fixture.Task.Id, "alpha", "skills/a/SKILL.md");
        await store.SaveForTaskAsync(fixture.Task.Id, [beta, alphaB, alphaA], fixture.Token);

        var rows = await store.ListForTaskAsync(fixture.Task.Id, fixture.Token);

        Assert.Equal([alphaA, alphaB, beta], rows);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task SaveReplacesThePriorSnapshotAtomically()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteTaskSkillActivationStore(fixture.Connections);
        await store.SaveForTaskAsync(fixture.Task.Id,
            [Row(fixture.Task.Id, "alpha", "skills/a/SKILL.md"), Row(fixture.Task.Id, "beta", "skills/b/SKILL.md")],
            fixture.Token);
        var replacement = Row(fixture.Task.Id, "gamma", "skills/g/SKILL.md");

        await store.SaveForTaskAsync(fixture.Task.Id, [replacement], fixture.Token);

        Assert.Equal([replacement], await store.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_activations;"));
    }

    [Fact]
    public async Task RowsAreScopedToTheirTask()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var other = new AgentTask(Guid.NewGuid(), fixture.Project.Id, "other task",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Failed, now, now, null);
        await fixture.Repository.SaveTaskAsync(other, fixture.Token);
        var store = new SqliteTaskSkillActivationStore(fixture.Connections);
        var mine = Row(fixture.Task.Id, "alpha", "skills/a/SKILL.md");
        await store.SaveForTaskAsync(fixture.Task.Id, [mine], fixture.Token);
        await store.SaveForTaskAsync(other.Id, [Row(other.Id, "beta", "skills/b/SKILL.md")], fixture.Token);

        Assert.Equal([mine], await store.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Single(await store.ListForTaskAsync(other.Id, fixture.Token));
        Assert.Empty(await store.ListForTaskAsync(Guid.NewGuid(), fixture.Token));
    }

    [Fact]
    public async Task DeletingTheTaskRowCascadesItsActivations()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteTaskSkillActivationStore(fixture.Connections);
        await store.SaveForTaskAsync(fixture.Task.Id, [Row(fixture.Task.Id, "alpha", "skills/a/SKILL.md")], fixture.Token);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_activations;"));

        await fixture.ExecuteAsync($"DELETE FROM agent_tasks WHERE id='{fixture.Task.Id}';");

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_activations;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task OversizedSkillTextIsRejectedByTheSchemaCheck()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteTaskSkillActivationStore(fixture.Connections);
        var oversized = Row(fixture.Task.Id, "alpha", "skills/a/SKILL.md", new string('x', 8193));

        var error = await Assert.ThrowsAsync<SqliteException>(() =>
            store.SaveForTaskAsync(fixture.Task.Id, [oversized], fixture.Token));

        Assert.Equal(19, error.SqliteErrorCode);
        Assert.Empty(await store.ListForTaskAsync(fixture.Task.Id, fixture.Token));
    }
}
