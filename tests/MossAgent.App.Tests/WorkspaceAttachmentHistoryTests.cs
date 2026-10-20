using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentHistoryTests
{
    [Fact]
    public async Task SubmittedTextSurvivesLaterTurnsSourceChangesAndIndependentComposition()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "history-attachment.txt");
        const string marker = "immutable-history-evidence-75193";
        await File.WriteAllTextAsync(path, marker, fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "first attachment turn";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        await File.WriteAllTextAsync(path, "changed-source-must-not-be-read", fixture.Token);

        workspace.ComposerText = "second turn without attachments";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        AssertAttachmentTurn(fixture.Server.Requests.Last(), marker, "second turn without attachments");
        workspace.Catalog.NewTaskCommand.Execute(null);
        workspace.Catalog.SelectTaskCommand.Execute(workspace.Catalog.Tasks.Single(item => item.Id == task.Id));
        await fixture.WaitUntilAsync(() => workspace.Messages.Any(message => message.IsUserMessage));
        Assert.Single(workspace.Messages.First(message => message.IsUserMessage).Attachments);
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);

        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        reopened.Catalog.SelectTaskCommand.Execute(reopened.Catalog.Tasks.Single(item => item.Id == task.Id));
        await fixture.WaitUntilAsync(() => reopened.Messages.Any(message => message.IsUserMessage));
        Assert.Single(reopened.Messages.First(message => message.IsUserMessage).Attachments);
        await reopened.Drafts.LoadCurrentAsync();
        reopened.ComposerText = "third turn after restart";
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 3 && !reopened.IsRunning);
        AssertAttachmentTurn(fixture.Server.Requests.Last(), marker, "third turn after restart");
        Assert.Equal(AgentTaskStatus.Completed, reopened.Catalog.SelectedTask!.Status);
        await reopened.ShutdownAsync();
    }

    [Fact]
    public async Task SwitchingTasksRestoresOnlyTheAttachmentsOwnedByEachConversation()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "task-owned.txt");
        await File.WriteAllTextAsync(path, "task-A-only-evidence", fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "turn A";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var first = workspace.Catalog.SelectedTask!;
        workspace.Catalog.NewTaskCommand.Execute(null);
        await File.WriteAllTextAsync(path, "task-B-only-evidence", fixture.Token);
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "turn B";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        var second = workspace.Catalog.SelectedTask!;
        Assert.NotEqual(first.Id, second.Id);
        var secondRequest = fixture.Server.Requests.Last().GetRawText();
        Assert.Contains("task-B-only-evidence", secondRequest);
        Assert.DoesNotContain("task-A-only-evidence", secondRequest);

        workspace.Catalog.SelectTaskCommand.Execute(first);
        await fixture.WaitUntilAsync(() => workspace.Messages.Any(message => message.Content == "turn A"));
        Assert.Single(workspace.Messages.First(message => message.IsUserMessage).Attachments);
        await workspace.Drafts.LoadCurrentAsync();
        workspace.ComposerText = "follow up A";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 3 && !workspace.IsRunning);
        var firstRequest = fixture.Server.Requests.Last().GetRawText();
        Assert.Contains("task-A-only-evidence", firstRequest);
        Assert.DoesNotContain("task-B-only-evidence", firstRequest);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
    }

    private static void AssertAttachmentTurn(JsonElement request, string marker, string latestPrompt)
    {
        var users = request.GetProperty("input").EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() == "user").ToArray();
        var original = Assert.Single(users, message => message.GetProperty("content").GetString()!.Contains(marker, StringComparison.Ordinal));
        Assert.StartsWith("first attachment turn", original.GetProperty("content").GetString());
        Assert.Contains("workspace_attachments", original.GetProperty("content").GetString());
        Assert.Equal(latestPrompt, users.Last().GetProperty("content").GetString());
        Assert.DoesNotContain("changed-source-must-not-be-read", request.GetRawText());
        Assert.Equal(1, users.Count(message => message.GetProperty("content").GetString()!.Contains("workspace_attachments", StringComparison.Ordinal)));
    }
}
