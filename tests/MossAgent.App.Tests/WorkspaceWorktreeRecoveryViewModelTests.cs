using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
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

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(15, timeout.Token);
    }
}
