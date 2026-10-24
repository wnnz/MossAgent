using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class RootObjectAuthorizationMigrationTests
{
    [Fact]
    public async Task ForwardMigrationPreservesDataRequiresConfirmationAndRollsBackFailure()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "retained history", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await fixture.ExecuteAsync("DROP TABLE worktree_root_authorizations; DROP TABLE project_root_authorizations; DELETE FROM schema_versions WHERE version>=27; CREATE TRIGGER reject_v27 BEFORE INSERT ON schema_versions WHEN NEW.version=27 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;");
        var failure = await Record.ExceptionAsync(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.True(failure is SqliteException or InvalidOperationException);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='project_root_authorizations';"));
        Assert.Equal(26, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_v27;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(31, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var project = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token));
        Assert.Empty(project.RootAuthorizations);
        Assert.Equal(fixture.Project.AuthorizationRevision, project.AuthorizationRevision);
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(project.Id, fixture.Token)));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO project_root_authorizations VALUES('{project.Id}',1,'not-json');"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
