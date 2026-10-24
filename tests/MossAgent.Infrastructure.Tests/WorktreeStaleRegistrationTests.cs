using MossAgent.Application.Git;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeStaleRegistrationTests : IAsyncLifetime
{
    private GitWorktreeSecurityFixture? _fixture;
    private SqliteFailurePersistenceFixture? _storage;
    private GitWorktreeSecurityFixture Fixture => _fixture!;
    private SqliteFailurePersistenceFixture Storage => _storage!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await GitWorktreeSecurityFixture.CreateAsync();
        try
        {
            _storage = await SqliteFailurePersistenceFixture.CreateAsync();
            await _storage.Repository.SaveProjectAsync(_fixture.Project, _storage.Token);
        }
        catch { _fixture.Dispose(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_storage is not null) await _storage.DisposeAsync();
        _fixture?.Dispose();
    }

    private GitWorktreeService Service() => new(Fixture.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()),
        roots: new RootIdentityProbe(), connections: Storage.Connections);

    [Fact]
    public async Task DeletesOnlyTheSlotProvenToPointAtTheMissingTarget()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var siblingId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var sibling = await service.CreateAsync(Fixture.Project, siblingId, Fixture.Token);
        var slot = AssertIsSlot(FindSlot(target));
        var siblingSlot = AssertIsSlot(FindSlot(sibling));
        await new SqliteWorktreeCleanupIntents(Storage.Connections)
            .RecordAsync(taskId, Fixture.Project, Fixture.Repository, target, Fixture.Token);
        await Storage.ExecuteAsync("""
            UPDATE worktree_cleanup_intents SET updated_at='2000-01-01T00:00:00.0000000+00:00'
            WHERE stage<3;
            """);
        DeleteExternally(target);
        await service.ReconcileAsync(Fixture.Token);

        await service.RemoveStaleRegistrationAsync(Fixture.Project, target, Fixture.Token);

        Assert.False(Directory.Exists(slot));
        Assert.True(Directory.Exists(siblingSlot));
        var listed = await Fixture.GitAsync(Fixture.Repository, "worktree", "list", "--porcelain");
        Assert.DoesNotContain(taskId.ToString("N"), listed);
        Assert.Contains(siblingId.ToString("N"), listed);
        await AssertCleanupRowAsync(taskId, WorktreeCleanupIntentStage.Orphaned, "removed");
    }

    [Fact]
    public async Task RefusesWhileTargetDirectoryStillExists()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var slot = AssertIsSlot(FindSlot(target));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoveStaleRegistrationAsync(Fixture.Project, target, Fixture.Token));

        Assert.True(Directory.Exists(target));
        Assert.True(Directory.Exists(slot));
        var listed = await Fixture.GitAsync(Fixture.Repository, "worktree", "list", "--porcelain");
        Assert.Contains(taskId.ToString("N"), listed);
    }

    [Fact]
    public async Task RefusesWhenSlotGitdirPointsElsewhere()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var slot = AssertIsSlot(FindSlot(target));
        DeleteExternally(target);
        var gitdir = Path.Combine(slot, "gitdir");
        File.SetAttributes(gitdir, FileAttributes.Normal);
        await File.WriteAllTextAsync(gitdir,
            Path.Combine(Fixture.Outside, "decoy", ".git") + "\n", Fixture.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoveStaleRegistrationAsync(Fixture.Project, target, Fixture.Token));

        Assert.True(Directory.Exists(slot));
        Assert.True(File.Exists(gitdir));
    }

    [Fact]
    public async Task RefusesWhenNoRegistrationSlotExists()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var slot = AssertIsSlot(FindSlot(target));
        DeleteExternally(target);
        Directory.Delete(slot, recursive: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoveStaleRegistrationAsync(Fixture.Project, target, Fixture.Token));

        Assert.False(Directory.Exists(slot));
    }

    [Fact]
    public async Task RefusesWhenMultipleSlotsPointAtTheTarget()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var slot = AssertIsSlot(FindSlot(target));
        DeleteExternally(target);
        var clone = slot + "-clone";
        CopyDirectory(slot, clone);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoveStaleRegistrationAsync(Fixture.Project, target, Fixture.Token));

        Assert.True(Directory.Exists(slot));
        Assert.True(Directory.Exists(clone));
    }

    private string AssertIsSlot(string? slot)
    {
        var found = Assert.IsType<string>(slot);
        Assert.Equal(Path.Combine(Fixture.Repository, ".git", "worktrees"),
            Path.GetDirectoryName(found));
        return found;
    }

    private static string? FindSlot(string repository, string target)
    {
        var registrations = Path.Combine(repository, ".git", "worktrees");
        if (!Directory.Exists(registrations)) return null;
        var expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(target, ".git")));
        foreach (var slot in Directory.EnumerateDirectories(registrations))
        {
            var gitdir = Path.Combine(slot, "gitdir");
            if (!File.Exists(gitdir)) continue;
            var content = File.ReadAllText(gitdir).Trim().Replace('/', Path.DirectorySeparatorChar);
            var resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                Path.IsPathFullyQualified(content) ? content : Path.Combine(slot, content)));
            if (string.Equals(resolved, expected, StringComparison.OrdinalIgnoreCase)) return slot;
        }
        return null;
    }

    private string? FindSlot(string target) => FindSlot(Fixture.Repository, target);

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, destination));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, destination));
    }

    private static void DeleteExternally(string directory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
            File.SetAttributes(entry, FileAttributes.Normal);
        Directory.Delete(directory, recursive: true);
    }

    private async Task AssertCleanupRowAsync(Guid taskId, WorktreeCleanupIntentStage stage, string? errorCode)
    {
        await using var connection = Storage.Connections.Create();
        await connection.OpenAsync(Fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT stage,error_code FROM worktree_cleanup_intents WHERE task_id=$task";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(Fixture.Token);
        Assert.True(await reader.ReadAsync(Fixture.Token));
        Assert.Equal((int)stage, reader.GetInt32(0));
        Assert.Equal(errorCode, reader.IsDBNull(1) ? null : reader.GetString(1));
    }
}
