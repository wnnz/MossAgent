using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeReauthorizationTests
{
    // Physically replaces the enrolled directory object: git removes the worktree, a new
    // worktree is registered at the same path, and the probe assigns a fresh identity.
    private static async Task ReplaceWorktreeAsync(WorktreeObjectAuthorizationFixture fixture, Guid taskId, string target)
    {
        await fixture.Git.GitAsync(fixture.Git.Repository, "worktree", "remove", "--force", target);
        await fixture.Git.GitAsync(fixture.Git.Repository, "worktree", "add", target, $"mossagent/{taskId:N}");
        fixture.Roots.Replace(target);
    }

    [Fact]
    public async Task ProbeReportsUnconfirmedForLegacyWorktreeWithoutGrant()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Git.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path, seedIntent: true);
        Assert.Equal(WorktreeEnrollmentState.Unconfirmed,
            await fixture.Service.ProbeAuthorizationAsync(fixture.Project, task, fixture.Token));
    }

    [Fact]
    public async Task ProbeReportsEnrolledWhileTheRegisteredObjectIsIntact()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        Assert.Equal(WorktreeEnrollmentState.Enrolled,
            await fixture.Service.ProbeAuthorizationAsync(fixture.Project, task, fixture.Token));
    }

    [Fact]
    public async Task ProbeReportsReplacedAfterTheDirectoryObjectIsRecreated()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        await ReplaceWorktreeAsync(fixture, id, path);
        Assert.Equal(WorktreeEnrollmentState.Replaced,
            await fixture.Service.ProbeAuthorizationAsync(fixture.Project, task, fixture.Token));
    }

    [Fact]
    public async Task ProbeReportsUnverifiableWhenTargetDirectoryIsMissing()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        await fixture.Git.GitAsync(fixture.Git.Repository, "worktree", "remove", "--force", path);
        Assert.False(Directory.Exists(path));
        Assert.Equal(WorktreeEnrollmentState.Unverifiable,
            await fixture.Service.ProbeAuthorizationAsync(fixture.Project, task, fixture.Token));
    }

    [Fact]
    public async Task ProbeReportsUnverifiableOutsideManagedScope()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        Assert.Equal(WorktreeEnrollmentState.Unverifiable,
            await fixture.Service.ProbeAuthorizationAsync(fixture.Project, task with { ProjectId = Guid.NewGuid() }, fixture.Token));
        Assert.Equal(WorktreeEnrollmentState.Unverifiable,
            await fixture.Service.ProbeAuthorizationAsync(fixture.Project, task with { WorktreePath = null }, fixture.Token));
        Assert.Equal(WorktreeEnrollmentState.Unverifiable,
            await fixture.Git.Service.ProbeAuthorizationAsync(fixture.Project, task, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationBindsTheReplacementObjectAndRestoresAdmission()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var before = Assert.IsType<WorktreeRootAuthorization>(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        await ReplaceWorktreeAsync(fixture, id, path);
        await fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token);
        var after = Assert.IsType<WorktreeRootAuthorization>(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        Assert.NotEqual(before.GrantId, after.GrantId);
        Assert.NotEqual(before.Identity.FileId, after.Identity.FileId);
        await using (var lease = await fixture.Service.AcquireAsync(fixture.Project, id, path, fixture.Token))
            Assert.NotNull(lease);
        Assert.Equal(0, fixture.Roots.ActiveSessions);
    }

    [Fact]
    public async Task ReauthorizationRefusesToProceedWithoutAnExistingGrant()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Git.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path, seedIntent: true);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token));
        Assert.Null(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationRefusesWhenTheEnrolledObjectIsStillIntact()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var grant = await fixture.Service.ReadAuthorizationAsync(id, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token));
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationRefusesWhileTheTaskIsRunning()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        await ReplaceWorktreeAsync(fixture, id, path);
        var grant = await fixture.Service.ReadAuthorizationAsync(id, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project,
                task with { Status = AgentTaskStatus.Running }, fixture.Token));
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationRejectsAStaleTaskSnapshot()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var grant = await fixture.Service.ReadAuthorizationAsync(id, fixture.Token);
        await ReplaceWorktreeAsync(fixture, id, path);
        await fixture.Storage.Repository.SaveTaskAsync(task with { UpdatedAt = task.UpdatedAt.AddSeconds(1) }, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token));
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationRejectsAStaleProjectAuthorizationRevision()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var grant = await fixture.Service.ReadAuthorizationAsync(id, fixture.Token);
        await ReplaceWorktreeAsync(fixture, id, path);
        await fixture.Storage.Repository.SaveProjectAsync(
            fixture.Project with { AuthorizedDirectories = [] }, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token));
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationIsDeniedWhileACleanupIntentIsLive()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var store = new SqliteWorktreeCleanupIntents(fixture.Storage.Connections);
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        await ReplaceWorktreeAsync(fixture, id, path);
        await store.RecordAsync(id, fixture.Project, fixture.Git.Repository, path, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationCannotBeRepeatedAfterTheFirstGrantRebind()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        await ReplaceWorktreeAsync(fixture, id, path);
        await fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token);
        var grant = await fixture.Service.ReadAuthorizationAsync(id, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token));
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task ReauthorizationRejectsAReplacementThatIsNotARegisteredWorktree()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var grant = await fixture.Service.ReadAuthorizationAsync(id, fixture.Token);
        await fixture.Git.GitAsync(fixture.Git.Repository, "worktree", "remove", "--force", path);
        Directory.CreateDirectory(path);
        fixture.Roots.Replace(path);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, fixture.Token));
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task CancelledReauthorizationLeavesTheGrantAndReleasesTheLateSession()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var grant = await fixture.Service.ReadAuthorizationAsync(id, fixture.Token);
        await ReplaceWorktreeAsync(fixture, id, path);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<IRootIdentitySession>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Roots.Override = (_, _) => { entered.TrySetResult(); return pending.Task; };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var reauthorization = fixture.Service.ReauthorizeReplacedAsync(fixture.Project, task, cancellation.Token);
        await entered.Task.WaitAsync(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reauthorization.WaitAsync(fixture.Token));
        var late = new RootIdentityProbeSession([new DirectoryObjectIdentity(path, "late-volume", "late-file")], () => { });
        pending.SetResult(late);
        await late.Disposed.Task.WaitAsync(fixture.Token);
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }
}
