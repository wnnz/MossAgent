using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreeRecoveryViewModelTests
{
    [Fact]
    public async Task EditorListsFailedIntentAndRequiresTwoStepAcknowledgement()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.Root, "data", "worktrees", taskId.ToString("N"));
        var connections = services.GetRequiredService<SqliteConnectionFactory>();
        var store = new SqliteWorktreeCreationIntentStore(connections, TimeProvider.System);
        await store.BeginAsync(project.Id, taskId, project.PrimaryDirectory, null, target, fixture.Token);
        await store.MarkFailedAsync(taskId, target, "task_submission_failed", fixture.Token);

        using var editor = new WorkspaceProjectEditorViewModel(
            services.GetRequiredService<WorkspaceProjectService>());
        editor.Edit(project);
        await WaitUntilAsync(() => editor.WorktreeRecovery.HasItems);
        var item = Assert.Single(editor.WorktreeRecovery.Items);
        Assert.True(item.CanAcknowledge);
        Assert.Contains("失败", item.StatusLabel);

        await item.AcknowledgeCommand.ExecuteAsync(null);
        Assert.Contains("确认", item.AcknowledgeLabel);
        Assert.Single(editor.WorktreeRecovery.Items);

        await item.AcknowledgeCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => !editor.WorktreeRecovery.HasItems);
        await using var connection = connections.Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM worktree_creation_intents WHERE task_id='{taskId}' AND status=6;";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync(fixture.Token)));
    }

    [Fact]
    public async Task EditorListsFailedCleanupWithQuarantinePathAndRequiresTwoStepAcknowledgement()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var taskId = Guid.NewGuid();
        var root = Path.Combine(fixture.Root, "data", "worktrees");
        var target = Path.Combine(root, taskId.ToString("N"));
        var quarantine = Path.Combine(root, $"{Guid.NewGuid():N}.quarantine");
        var store = services.GetRequiredService<IWorktreeCleanupIntentService>();
        await store.BeginAsync(project.Id, taskId, target, quarantine, fixture.Token);
        await store.MarkFailedAsync(taskId, quarantine,
            "worktree_cleanup_failed", fixture.Token);

        using var editor = new WorkspaceProjectEditorViewModel(
            services.GetRequiredService<WorkspaceProjectService>());
        editor.Edit(project);
        await WaitUntilAsync(() => editor.WorktreeRecovery.HasItems);
        var item = Assert.Single(editor.WorktreeRecovery.Items);
        Assert.Equal(WorktreeCleanupState.Failed, item.CleanupIntent?.State);
        Assert.Contains("清理失败", item.StatusLabel);
        Assert.Contains(quarantine, item.QuarantineLabel);
        Assert.True(item.CanAcknowledge);

        await item.AcknowledgeCommand.ExecuteAsync(null);
        Assert.Contains("确认", item.AcknowledgeLabel);
        await item.AcknowledgeCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => !editor.WorktreeRecovery.HasItems);

        var connections = services.GetRequiredService<SqliteConnectionFactory>();
        await using var connection = connections.Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM worktree_cleanup_intents WHERE task_id='{taskId}' AND state=3;";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync(fixture.Token)));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(15, timeout.Token);
    }
}
