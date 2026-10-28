using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteTaskSkillOverridesTests
{
    private static TaskSkillOverride Row(Guid taskId, Guid pluginId, string path, bool enabled) =>
        new(taskId, pluginId, path, enabled);

    private static AgentTask Task(SqliteFailurePersistenceFixture fixture, string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(Guid.NewGuid(), fixture.Project.Id, title, ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Failed, now, now, null);
    }

    [Fact]
    public async Task AbsentRowsReportAnEmptyChoiceList()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);

        Assert.Empty(await overrides.ListForTaskAsync(fixture.Task.Id, fixture.Token));
    }

    [Fact]
    public async Task ChoicesRoundTripAndUpsertKeepsASingleRow()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);
        var pluginId = Guid.NewGuid();

        await overrides.SetSkillEnabledAsync(fixture.Task.Id, pluginId, "skills/a/SKILL.md",
            false, fixture.Token);
        Assert.Equal([Row(fixture.Task.Id, pluginId, "skills/a/SKILL.md", false)],
            await overrides.ListForTaskAsync(fixture.Task.Id, fixture.Token));

        await overrides.SetSkillEnabledAsync(fixture.Task.Id, pluginId, "skills/a/SKILL.md",
            true, fixture.Token);
        Assert.Equal([Row(fixture.Task.Id, pluginId, "skills/a/SKILL.md", true)],
            await overrides.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_overrides;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task OverridesAreScopedPerTaskForTheSamePluginAndPath()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);
        var other = Task(fixture, "other task");
        await fixture.Repository.SaveTaskAsync(other, fixture.Token);
        var pluginId = Guid.NewGuid();

        await overrides.SetSkillEnabledAsync(fixture.Task.Id, pluginId, "skills/a/SKILL.md",
            false, fixture.Token);
        await overrides.SetSkillEnabledAsync(other.Id, pluginId, "skills/a/SKILL.md",
            true, fixture.Token);

        Assert.Equal([Row(fixture.Task.Id, pluginId, "skills/a/SKILL.md", false)],
            await overrides.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal([Row(other.Id, pluginId, "skills/a/SKILL.md", true)],
            await overrides.ListForTaskAsync(other.Id, fixture.Token));
    }

    [Fact]
    public async Task ClearForTaskDeletesOnlyThatTasksRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);
        var other = Task(fixture, "other task");
        await fixture.Repository.SaveTaskAsync(other, fixture.Token);
        var pluginId = Guid.NewGuid();
        await overrides.SetSkillEnabledAsync(fixture.Task.Id, pluginId, "skills/a/SKILL.md",
            false, fixture.Token);
        await overrides.SetSkillEnabledAsync(fixture.Task.Id, pluginId, "skills/b/SKILL.md",
            true, fixture.Token);
        await overrides.SetSkillEnabledAsync(other.Id, pluginId, "skills/a/SKILL.md",
            false, fixture.Token);

        await overrides.ClearForTaskAsync(fixture.Task.Id, fixture.Token);

        Assert.Empty(await overrides.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Single(await overrides.ListForTaskAsync(other.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task DeletingTheTaskRowCascadesItsOverrides()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);
        await overrides.SetSkillEnabledAsync(fixture.Task.Id, Guid.NewGuid(), "skills/a/SKILL.md",
            false, fixture.Token);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_overrides;"));

        await fixture.ExecuteAsync($"DELETE FROM agent_tasks WHERE id='{fixture.Task.Id}';");

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_overrides;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task WritingAgainstAMissingTaskFailsOnTheForeignKey()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);

        var error = await Assert.ThrowsAsync<SqliteException>(() =>
            overrides.SetSkillEnabledAsync(Guid.NewGuid(), Guid.NewGuid(), "skills/a/SKILL.md",
                false, fixture.Token));

        Assert.Equal(19, error.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_overrides;"));
    }

    [Fact]
    public async Task UninstalledPluginIdsStayWritableAsInertHistory()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);
        var orphanedPlugin = Guid.NewGuid();

        // The plugin id carries no foreign key: rows for uninstalled plugins must still
        // write and read back rather than fail, because reinstall assigns a new row id.
        await overrides.SetSkillEnabledAsync(fixture.Task.Id, orphanedPlugin, "skills/a/SKILL.md",
            false, fixture.Token);

        Assert.Equal([Row(fixture.Task.Id, orphanedPlugin, "skills/a/SKILL.md", false)],
            await overrides.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task EnabledValuesOutsideZeroOneAreRejectedByTheSchemaCheck()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();

        var error = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO task_skill_overrides VALUES('{fixture.Task.Id}','{Guid.NewGuid()}','skills/a/SKILL.md',2);"));

        Assert.Equal(19, error.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM task_skill_overrides;"));
    }
}
