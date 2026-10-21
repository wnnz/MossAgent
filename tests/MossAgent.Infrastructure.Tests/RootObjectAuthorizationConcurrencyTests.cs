using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class RootObjectAuthorizationConcurrencyTests
{
    [Fact]
    public async Task ConcurrentScopeSaveWinsOverPendingConfirmationWithoutHalfEnrollmentOrNotification()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var probe = new RootIdentityProbe();
        var changes = new ProjectAuthorizationChanges();
        var repository = new SqliteWorkspaceRepository(fixture.Connections, authorizationChanges: changes, rootIdentities: probe);
        var initial = await repository.ConfirmRootObjectsAsync(fixture.Project, fixture.Token);
        var native = await probe.OpenAsync(initial.AuthorizedDirectories, fixture.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IRootIdentitySession>(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Override = (_, _) => { entered.TrySetResult(); return release.Task; };
        var notifications = new List<long>();
        changes.Changed += (_, revision) => notifications.Add(revision);
        var pending = repository.ConfirmRootObjectsAsync(initial, fixture.Token);
        await entered.Task.WaitAsync(fixture.Token);
        probe.Override = null;
        var saved = await repository.SaveAuthorizationAsync(initial with { AuthorizedDirectories = [initial.PrimaryDirectory, "additional"] }, fixture.Token);
        release.SetResult(native);
        await Assert.ThrowsAsync<ProjectAuthorizationConflictException>(() => pending);
        var actual = (await repository.ReadAuthorizationAsync(initial.Id, fixture.Token))!;
        Assert.Equal(saved.AuthorizationRevision, actual.AuthorizationRevision);
        Assert.Equal(saved.RootAuthorizations, actual.RootAuthorizations);
        Assert.Equal(new[] { saved.AuthorizationRevision }, notifications);
        Assert.Equal(0, probe.ActiveSessions);
    }
}
