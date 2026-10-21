using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class RootObjectAuthorizationCancellationTests
{
    [Fact]
    public async Task UncooperativeIdentityReaderCannotHoldCancellationAndLateSessionIsDisposed()
    {
        var result = new TaskCompletionSource<IRootIdentitySession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new RootIdentityProbe { Override = (_, _) => result.Task };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = RootIdentitySessionOperation.OpenAsync(factory, ["synthetic"], cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        var session = new RootIdentityProbeSession([], () => { });
        result.SetResult(session);
        await session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncorrectIdentityResponseCannotBePersisted(bool wrongPath)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var identities = wrongPath ? new[] { new DirectoryObjectIdentity("wrong", "volume", "file") } : [];
        var session = new RootIdentityProbeSession(identities, () => { });
        var factory = new RootIdentityProbe { Override = (_, _) => Task.FromResult<IRootIdentitySession>(session) };
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => ProjectRootEnrollment.PrepareAsync(
            factory, fixture.Project, fixture.Project, true, fixture.Token));
        await session.Disposed.Task.WaitAsync(fixture.Token);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM project_root_authorizations;"));
    }

    [Theory]
    [InlineData("volume")]
    [InlineData("file")]
    [InlineData("duplicate")]
    public async Task InvalidObjectIdentityOrDuplicateRootsAreNotAnEnrollment(string invalid)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var project = fixture.Project;
        var identity = new DirectoryObjectIdentity(project.PrimaryDirectory,
            invalid == "volume" ? "" : "volume", invalid == "file" ? " " : "file");
        if (invalid == "duplicate") project = project with { AuthorizedDirectories = [project.PrimaryDirectory, project.PrimaryDirectory] };
        var session = new RootIdentityProbeSession(invalid == "duplicate" ? [identity, identity] : [identity], () => { });
        var factory = new RootIdentityProbe { Override = (_, _) => Task.FromResult<IRootIdentitySession>(session) };
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => ProjectRootEnrollment.PrepareAsync(
            factory, null, project, true, fixture.Token));
        await session.Disposed.Task.WaitAsync(fixture.Token);
    }
}
