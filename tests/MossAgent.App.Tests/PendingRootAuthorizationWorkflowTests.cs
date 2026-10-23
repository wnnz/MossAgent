using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Authorization;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class PendingRootAuthorizationWorkflowTests
{
    [Fact]
    public async Task CancelledToolKeepsLateNativeRootsVisibleUntilCleanupAndNeverReplaysTheRead()
    {
        var roots = new PendingRootIdentitySessionFactory(new WindowsRootIdentitySessionFactory());
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused",
            configure: services => services.Replace(ServiceDescriptor.Singleton<IRootIdentitySessionFactory>(roots)));
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var repository = fixture.Services.GetRequiredService<IWorkspaceRepository>();
        var task = new AgentTask(Guid.NewGuid(), project.Id, "pending roots", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        await repository.SaveTaskAsync(task, fixture.Token);
        var panels = fixture.Services.GetRequiredService<WorkspaceTaskPanelCoordinator>();
        var context = panels.CreateContext(task, project);
        var executor = fixture.Services.GetRequiredService<IToolExecutor>();
        var monitor = fixture.Services.GetRequiredService<ProjectAuthorizationMonitor>();
        roots.Arm();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var pending = ReadAsync(executor, context, fixture.FilePath, "cancelled-root-read", cancellation.Token);
        try
        {
            await roots.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            await cancellation.CancelAsync();
            var cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token);
            Assert.Equal("tool_cancelled", cancelled.ErrorCode);
            Assert.Null(cancelled.Content);
            Assert.False((await monitor.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
                TimeSpan.FromMilliseconds(75), fixture.Token)).IsDrained);
            fixture.Workspace.ComposerText = "foreground draft remains";
            using var editor = new WorkspaceProjectEditorViewModel(new WorkspaceProjectService(repository,
                new TestFolderPickerService(fixture.Root), monitor, TimeSpan.FromMilliseconds(75)));
            editor.Edit(project);
            await editor.AddDirectoryCommand.ExecuteAsync(null);
            await editor.AddCommand.ExecuteAsync(null);
            Assert.True(editor.IsOpen);
            Assert.True(editor.HasStatusError);
            Assert.Contains("授权已保存", editor.Status);
            var saved = Assert.Single(await repository.GetProjectsAsync(fixture.Token));
            Assert.True(saved.AuthorizationRevision > project.AuthorizationRevision);
            Assert.Equal("foreground draft remains", fixture.Workspace.ComposerText);
            roots.Release();
            await roots.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.True((await monitor.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
                TimeSpan.FromSeconds(5), fixture.Token)).IsDrained);
            Assert.Equal("workspace_authorization_changed",
                (await ReadAsync(executor, context, fixture.FilePath, "stale-root-read", fixture.Token)).ErrorCode);
            var fresh = await ReadAsync(executor, panels.CreateContext(task, saved), fixture.FilePath,
                "fresh-root-read", fixture.Token);
            Assert.True(fresh.IsSuccess, fresh.Summary);
            Assert.Contains("alpha", fresh.Content);
            Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
            var audits = await fixture.Services.GetRequiredService<IToolExecutionAuditReader>()
                .GetRecentAsync(task.Id, 10, fixture.Token);
            Assert.Equal(3, audits.Count);
            Assert.All(audits, audit => Assert.NotNull(audit.CompletedAt));
            Assert.Equal("tool_cancelled", Assert.Single(audits, audit => audit.CallId == "cancelled-root-read").ErrorCode);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { roots.Release(); await pending.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token); }
    }

    private static Task<ToolResult> ReadAsync(IToolExecutor executor, ToolExecutionContext context, string path,
        string callId, CancellationToken token) => executor.ExecuteAsync(new(callId, "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path })), context, token);
}
