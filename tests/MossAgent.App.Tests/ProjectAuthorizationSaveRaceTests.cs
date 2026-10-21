using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Authorization;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectAuthorizationSaveRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveReturnsItsCommittedRevisionAndCannotAdoptAnotherWindowsAuthorization(bool create)
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var original = fixture.Workspace.Catalog.SelectedProject!;
        var ownRevision = create ? 1 : 2;
        var changes = services.GetRequiredService<IProjectAuthorizationChanges>();
        var external = new SqliteWorkspaceRepository(services.GetRequiredService<SqliteConnectionFactory>());
        ProjectProfile? externalSaved = null;
        Exception? callbackFailure = null;
        void SaveFromOtherWindow(Guid id, long revision)
        {
            if (revision != ownRevision || (create ? id == original.Id : id != original.Id)) return;
            try
            {
                var committed = external.ReadAuthorizationAsync(id, fixture.Token).GetAwaiter().GetResult()!;
                external.SaveProjectAsync(committed with { AuthorizedDirectories = [fixture.ProjectRoot] }, fixture.Token)
                    .GetAwaiter().GetResult();
                externalSaved = external.ReadAuthorizationAsync(id, fixture.Token).GetAwaiter().GetResult();
            }
            catch (Exception error) { callbackFailure = error; }
        }
        changes.Changed += SaveFromOtherWindow;
        ProjectProfile saved;
        try
        {
            var projects = services.GetRequiredService<WorkspaceProjectService>();
            saved = create
                ? await projects.CreateAsync("new local draft", [fixture.ProjectRoot, fixture.Root], fixture.Token)
                : await projects.UpdateAsync(original, "local draft", [fixture.ProjectRoot, fixture.Root], fixture.Token);
        }
        finally { changes.Changed -= SaveFromOtherWindow; }

        Assert.Null(callbackFailure);
        Assert.NotNull(externalSaved);
        Assert.Equal(ownRevision + 1, externalSaved.AuthorizationRevision);
        Assert.Equal(new[] { fixture.ProjectRoot }, externalSaved.AuthorizedDirectories);
        Assert.Equal(ownRevision, saved.AuthorizationRevision);
        Assert.Equal(new[] { fixture.ProjectRoot, fixture.Root }, saved.AuthorizedDirectories);
        await Assert.ThrowsAsync<ProjectAuthorizationConflictException>(() => services.GetRequiredService<WorkspaceProjectService>()
            .UpdateAsync(saved, "stale draft", saved.AuthorizedDirectories, fixture.Token));
        Assert.Equal(externalSaved.AuthorizationRevision,
            (await external.ReadAuthorizationAsync(saved.Id, fixture.Token))!.AuthorizationRevision);
        Assert.Empty(fixture.Server.Requests);
    }
}
