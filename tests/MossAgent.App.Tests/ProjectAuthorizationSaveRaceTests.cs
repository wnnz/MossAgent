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
    [Fact]
    public async Task SaveWaitsForRevokedOperationToExitBeforeReturning()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var original = fixture.Workspace.Catalog.SelectedProject!;
        var monitor = services.GetRequiredService<ProjectAuthorizationMonitor>();
        var lease = await monitor.AcquireAsync(original, fixture.Token);
        var revoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Task.Run(async () =>
        {
            try { await Task.Delay(Timeout.Infinite, lease.Revocation); }
            catch (OperationCanceledException) when (lease.IsRevoked) { }
            revoked.TrySetResult();
            await release.Task;
            await lease.DisposeAsync();
        }, fixture.Token);
        try
        {
            var saving = services.GetRequiredService<WorkspaceProjectService>()
                .UpdateAsync(original, original.Name, [fixture.ProjectRoot, fixture.Root], fixture.Token);
            await revoked.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.False(saving.IsCompleted);
            release.TrySetResult();
            var saved = await saving.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.True(saved.AuthorizationRevision > original.AuthorizationRevision);
            await operation.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        }
        finally
        {
            release.TrySetResult();
            await lease.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        }
    }

    [Fact]
    public async Task EditorPublishesSavedRevisionAndWarnsWhenRevocationDoesNotDrain()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var original = fixture.Workspace.Catalog.SelectedProject!;
        var monitor = services.GetRequiredService<ProjectAuthorizationMonitor>();
        var lease = await monitor.AcquireAsync(original, fixture.Token);
        try
        {
            var projectService = new WorkspaceProjectService(
                services.GetRequiredService<IWorkspaceRepository>(),
                new TestFolderPickerService(fixture.Root),
                monitor,
                TimeSpan.FromMilliseconds(25));
            using var editor = new WorkspaceProjectEditorViewModel(projectService);
            ProjectProfile? published = null;
            editor.ProjectUpdated += project => published = project;
            editor.Edit(original);
            await editor.AddDirectoryCommand.ExecuteAsync(null);
            await editor.AddCommand.ExecuteAsync(null);

            Assert.True(editor.IsOpen);
            Assert.True(editor.HasStatusError);
            Assert.Equal("项目授权已保存，但旧运行或工具仍在停止。请等待任务结束后再继续敏感操作。", editor.Status);
            Assert.NotNull(published);
            Assert.True(published.AuthorizationRevision > original.AuthorizationRevision);
            Assert.Equal(published.AuthorizationRevision,
                (await services.GetRequiredService<IProjectAuthorizationReader>()
                    .ReadAuthorizationAsync(original.Id, fixture.Token))!.AuthorizationRevision);
        }
        finally { await lease.DisposeAsync(); }
    }

    [Fact]
    public async Task RootConfirmationUsesTheSameBoundedRevocationBarrier()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var original = fixture.Workspace.Catalog.SelectedProject!;
        var monitor = services.GetRequiredService<ProjectAuthorizationMonitor>();
        var lease = await monitor.AcquireAsync(original, fixture.Token);
        try
        {
            var projectService = new WorkspaceProjectService(
                services.GetRequiredService<IWorkspaceRepository>(),
                new TestFolderPickerService(fixture.Root),
                monitor,
                TimeSpan.FromMilliseconds(25));
            using var editor = new WorkspaceProjectEditorViewModel(projectService);
            ProjectProfile? published = null;
            editor.ProjectUpdated += project => published = project;
            editor.Edit(original);
            await editor.ConfirmRootObjectsCommand.ExecuteAsync(null);

            Assert.True(editor.IsOpen);
            Assert.True(editor.HasStatusError);
            Assert.Equal("项目授权已保存，但旧运行或工具仍在停止。请等待任务结束后再继续敏感操作。", editor.Status);
            Assert.NotNull(published);
            Assert.True(published.AuthorizationRevision > original.AuthorizationRevision);
        }
        finally { await lease.DisposeAsync(); }
    }

    [Fact]
    public async Task EditorRequiresTwoStepConfirmationBeforeAbandoningAStaleRegistration()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var original = fixture.Workspace.Catalog.SelectedProject!;
        var registry = services.GetRequiredService<IAuthorizationResourceRegistry>();
        var leaseId = Guid.NewGuid();
        await registry.RegisterAsync(leaseId, original.Id, original.AuthorizationRevision, fixture.Token);
        var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-31);
        await using (var connection = services.GetRequiredService<SqliteConnectionFactory>().Create())
        {
            await connection.OpenAsync(fixture.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE authorization_resource_leases SET heartbeat_at=$heartbeat WHERE lease_id=$lease;";
            command.Parameters.AddWithValue("$heartbeat", staleBefore.AddMinutes(-1).ToString("O"));
            command.Parameters.AddWithValue("$lease", leaseId.ToString());
            await command.ExecuteNonQueryAsync(fixture.Token);
        }

        using var editor = new WorkspaceProjectEditorViewModel(
            services.GetRequiredService<WorkspaceProjectService>());
        editor.Edit(original);
        await WaitUntilAsync(() => editor.AuthorizationResources.HasStaleResources);
        Assert.Equal(1, editor.AuthorizationResources.StaleCount);
        Assert.True(editor.AuthorizationResources.AbandonCommand.CanExecute(null));

        await editor.AuthorizationResources.AbandonCommand.ExecuteAsync(null);
        Assert.Contains("确认", editor.AuthorizationResources.AbandonLabel);
        Assert.True(editor.AuthorizationResources.HasOutstandingResources);

        await editor.AuthorizationResources.AbandonCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => !editor.AuthorizationResources.HasOutstandingResources);
        await using var verification = services.GetRequiredService<SqliteConnectionFactory>().Create();
        await verification.OpenAsync(fixture.Token);
        await using var audit = verification.CreateCommand();
        audit.CommandText = "SELECT COUNT(*) FROM authorization_resource_leases WHERE resolution='abandoned';";
        Assert.Equal(1L, Convert.ToInt64(await audit.ExecuteScalarAsync(fixture.Token)));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(15, timeout.Token);
    }

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
