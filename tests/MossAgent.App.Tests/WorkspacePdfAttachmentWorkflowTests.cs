using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePdfAttachmentWorkflowTests
{
    [Fact]
    public async Task PdfDraftSnapshotAndHistoryReachNativeRequestsAfterSourceDeletion()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await EnableFilesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(
            fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        Assert.True(workspace.SelectedModel!.SupportsFiles);
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "selected-report.pdf");
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        await File.WriteAllBytesAsync(path, bytes, fixture.Token);
        picker.Paths = [path];

        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        var pending = Assert.Single(workspace.Attachments.Items);
        Assert.Equal("application/pdf", pending.Content.MediaType);
        Assert.NotNull(pending.Content.Document);
        Assert.Null(pending.Thumbnail);
        await workspace.Drafts.FlushAsync();
        workspace.Catalog.NewChatCommand.Execute(null);
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Empty(workspace.Attachments.Items);
        workspace.Catalog.SelectedProject = project;
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Single(workspace.Attachments.Items);

        workspace.ComposerText = "read the PDF before restart";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        AssertNativeFile(fixture.Server.Requests.Single(), bytes);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);

        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        reopened.Catalog.SelectTaskCommand.Execute(
            reopened.Catalog.Tasks.Single(task => task.Id == taskId));
        await fixture.WaitUntilAsync(() => reopened.Messages.Any(message => message.IsUserMessage));
        Assert.Single(reopened.Messages.First(message => message.IsUserMessage).Attachments);
        reopened.ComposerText = "answer from PDF history";
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !reopened.IsRunning);

        AssertNativeFile(fixture.Server.Requests.Last(), bytes);
        await reopened.ShutdownAsync();
    }

    private static async Task EnableFilesAsync(WorkspaceModelSelectionFixture fixture)
    {
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var editor = fixture.Main.Models;
        editor.EditProviderModelCommand.Execute(fixture.SelectedModel);
        editor.SupportsFiles = true;
        await editor.SaveModelCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
    }

    private static void AssertNativeFile(JsonElement request, byte[] bytes)
    {
        var turns = request.GetProperty("input").EnumerateArray()
            .Where(message => message.GetProperty("content").ValueKind == JsonValueKind.Array)
            .ToArray();
        var blocks = turns.SelectMany(turn => turn.GetProperty("content").EnumerateArray()).ToArray();
        var block = Assert.Single(blocks,
            item => item.TryGetProperty("type", out var type) && type.GetString() == "input_file");
        Assert.Equal("selected-report.pdf", block.GetProperty("filename").GetString());
        Assert.Equal("data:application/pdf;base64," + Convert.ToBase64String(bytes),
            block.GetProperty("file_data").GetString());
        Assert.DoesNotContain("workspace_attachments", request.GetRawText());
    }
}
