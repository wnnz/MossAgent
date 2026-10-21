using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class RootObjectAuthorizationRuntimeTests
{
    [Fact]
    public async Task ReplacementIsDeniedUntilExplicitConfirmationAndOldContextNeverRegainsAccess()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "root identity", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var repository = fixture.Services.GetRequiredService<IWorkspaceRepository>();
        await repository.SaveTaskAsync(task, fixture.Token);
        var panels = fixture.Services.GetRequiredService<WorkspaceTaskPanelCoordinator>();
        var context = panels.CreateContext(task, project);
        await RootObjectTestDirectory.MoveWhenReadersFinishAsync(fixture.ProjectRoot, fixture.ProjectRoot + "-original", fixture.Token);
        Directory.CreateDirectory(fixture.ProjectRoot);
        await File.WriteAllTextAsync(fixture.FilePath, "replacement-sensitive", fixture.Token);
        var executor = fixture.Services.GetRequiredService<IToolExecutor>();
        var denied = await ReadAsync(executor, context, fixture.FilePath, fixture.Token);
        Assert.Equal("workspace_authorization_changed", denied.ErrorCode);
        Assert.DoesNotContain("replacement-sensitive", denied.Content ?? string.Empty);
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => fixture.Services.GetRequiredService<WorkspaceProjectService>()
            .UpdateAsync(project, "rename must not authorize", project.AuthorizedDirectories, fixture.Token));
        fixture.Workspace.ComposerText = "preserved foreground draft";
        var editor = fixture.Workspace.Catalog.ProjectEditor;
        editor.Edit(project);
        editor.Name = "preserved editor draft";
        await editor.ConfirmRootObjectsCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
        Assert.True(editor.IsOpen);
        Assert.Equal("preserved editor draft", editor.Name);
        Assert.Equal("preserved foreground draft", fixture.Workspace.ComposerText);
        var saved = Assert.Single(await repository.GetProjectsAsync(fixture.Token));
        Assert.Equal(2, saved.AuthorizationRevision);
        Assert.NotEqual(project.RootAuthorizations[0].Identity.FileId, saved.RootAuthorizations[0].Identity.FileId);
        Assert.Equal("workspace_authorization_changed", (await ReadAsync(executor, context, fixture.FilePath, fixture.Token)).ErrorCode);
        var read = await ReadAsync(executor, panels.CreateContext(task, saved), fixture.FilePath, fixture.Token);
        Assert.True(read.IsSuccess, read.Summary);
        Assert.Contains("replacement-sensitive", read.Content);
        var audits = await fixture.Services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(task.Id, 10, fixture.Token);
        Assert.Equal(3, audits.Count);
        Assert.All(audits, audit => Assert.NotNull(audit.CompletedAt));
        Assert.Equal(2, audits.Count(audit => audit.ErrorCode == "workspace_authorization_changed"));
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(Path.Combine(fixture.ProjectRoot + "-original", "fixture.txt"), fixture.Token));
        Assert.Empty(fixture.Server.Requests);
    }

    private static Task<ToolResult> ReadAsync(IToolExecutor executor, ToolExecutionContext context, string path,
        CancellationToken token) => executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path })), context, token);
}
