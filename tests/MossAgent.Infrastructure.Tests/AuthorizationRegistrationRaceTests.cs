using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AuthorizationRegistrationRaceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RevisionChangeAroundRegistrationRejectsBeforeAnyNativeRootOpen(bool acquire, bool pauseAfterRegistration)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var roots = new RootIdentityProbe();
        var repository = new SqliteWorkspaceRepository(fixture.Connections, rootIdentities: roots);
        var project = await repository.ConfirmRootObjectsAsync(fixture.Project, fixture.Token);
        using var captured = await roots.OpenAsync(project.AuthorizedDirectories, fixture.Token);
        var nativeOpens = 0;
        roots.Override = (_, _) =>
        {
            Interlocked.Increment(ref nativeOpens);
            return Task.FromResult<IRootIdentitySession>(new RootIdentityProbeSession(captured.Roots, () => { }));
        };
        var registry = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var gate = new AuthorizationResourceRegistrationGate(registry, pauseAfterRegistration);
        await using var monitor = new ProjectAuthorizationMonitor(repository, new ProjectAuthorizationChanges(), roots: roots, resources: gate);
        await using var peer = new ProjectAuthorizationMonitor(repository, new ProjectAuthorizationChanges(), resources:
            new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System));
        var pending = ValidateOrAcquireAsync(monitor, project, acquire, fixture.Token);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            await fixture.Repository.SaveProjectAsync(project with { AuthorizedDirectories = [] }, fixture.Token);
            var drain = await peer.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
                TimeSpan.FromMilliseconds(75), fixture.Token);
            Assert.Equal(!pauseAfterRegistration, drain.IsDrained);
            Assert.Equal(pauseAfterRegistration ? 1 : 0, drain.OutstandingOperations);
            gate.Continue.TrySetResult();
            await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token));
            Assert.Equal(0, Volatile.Read(ref nativeOpens));
            Assert.Empty(await registry.ListOutstandingAsync(project.Id, fixture.Token));
            Assert.True((await peer.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
                TimeSpan.FromSeconds(2), fixture.Token)).IsDrained);
        }
        finally
        {
            gate.Continue.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token); }
            catch (WorkspaceAuthorizationException) { }
        }
    }

    private static async Task ValidateOrAcquireAsync(ProjectAuthorizationMonitor monitor, ProjectProfile project,
        bool acquire, CancellationToken cancellationToken)
    {
        if (acquire) { await using var lease = await monitor.AcquireAsync(project, cancellationToken); }
        else await monitor.ValidateAsync(project, cancellationToken);
    }
}
