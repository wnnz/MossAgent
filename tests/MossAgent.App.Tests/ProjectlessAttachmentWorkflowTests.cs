using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectlessAttachmentWorkflowTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitFilesSurviveDraftNavigationReopenAndFollowUpWithoutCreatingAProjectOrAdvertisingTools(bool image)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        if (image) await EnableImagesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        Assert.Null(workspace.Catalog.SelectedProject);
        Assert.True(workspace.Attachments.AddFilesCommand.CanExecute(null));
        var path = Path.Combine(Path.GetDirectoryName(fixture.DataRoot)!, image ? "selected.png" : "selected.txt");
        await File.WriteAllBytesAsync(path, image ? Convert.FromBase64String(Png) : "selected-file-content"u8.ToArray(), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(null);
        var chip = Assert.Single(workspace.Attachments.Items);
        Assert.Equal(Path.GetFileName(path), chip.DisplayPath);
        Assert.Equal(WorkspaceAttachmentAuthorization.CreateSelection(path), chip.AuthorizationStamp);
        workspace.ComposerText = image ? string.Empty : "read the selected text";
        await workspace.Drafts.FlushAsync();
        var now = DateTimeOffset.UtcNow;
        var other = new AgentTask(Guid.NewGuid(), null, "other plain chat", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(other, fixture.Token);
        await workspace.Catalog.ReloadAsync();
        workspace.Catalog.SelectTaskCommand.Execute(other);
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Empty(workspace.Attachments.Items);
        workspace.Catalog.NewChatCommand.Execute(null);
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Single(workspace.Attachments.Items);
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);
        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        Assert.Null(reopened.Catalog.SelectedProject);
        Assert.Equal(other.Id, reopened.Catalog.SelectedTask?.Id);
        Assert.Empty(reopened.Attachments.Items);
        reopened.Catalog.NewChatCommand.Execute(null);
        await reopened.Drafts.LoadCurrentAsync();
        Assert.Single(reopened.Attachments.Items);
        Assert.True(reopened.PrimaryActionCommand.CanExecute(null));
        reopened.ApprovalPolicy = ApprovalPolicy.FullAccess;
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !reopened.IsRunning);
        AssertRequest(fixture.Server.Requests.Single(), image, path);
        var task = reopened.Catalog.SelectedTask!;
        Assert.Null(task.ProjectId);
        Assert.Null(task.WorktreePath);
        Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        Assert.Empty(reopened.Attachments.Items);
        reopened.ComposerText = "follow up after the selected file was deleted";
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !reopened.IsRunning);
        AssertRequest(fixture.Server.Requests.Last(), image, path);
        Assert.Empty(await fixture.Tasks.GetProjectsAsync(fixture.Token));
        var stored = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal(image ? "image/png" : "text/plain", Assert.Single(stored.First(message => message.Role == MessageRole.User).Attachments!).MediaType);
        await reopened.ShutdownAsync();
    }

    [Fact]
    public async Task UnsupportedModelRetainsProjectlessImageAndBecomesSendableAfterItsCapabilityIsEnabled()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var path = Path.Combine(fixture.DataRoot, "unsupported.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(Png), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(null);
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.False(workspace.Attachments.IsModelCompatible);
        await workspace.Drafts.FlushAsync();
        workspace.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);
        await EnableImagesAsync(fixture);
        await workspace.ModelPicker.ReloadAsync();
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.Equal(Png, Assert.Single(workspace.Attachments.Items).Content.Image?.Base64Data);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        AssertRequest(fixture.Server.Requests.Single(), true, path);
        await workspace.ShutdownAsync();
    }

    [Fact]
    public async Task SubmittedProjectlessImagesRestoreAfterCompositionReopenAndStillAdvertiseNoTools()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        await EnableImagesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var path = Path.Combine(fixture.DataRoot, "history.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(Png), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        AssertRequest(fixture.Server.Requests.Single(), true, path);
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);
        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        await fixture.WaitUntilAsync(() => reopened.Messages.Any(message => message.IsUserMessage));
        Assert.Null(reopened.Catalog.SelectedProject);
        Assert.Equal(taskId, reopened.Catalog.SelectedTask?.Id);
        Assert.Single(reopened.Messages.First(message => message.IsUserMessage).Attachments);
        reopened.ComposerText = "continue with the stored image";
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !reopened.IsRunning);
        AssertRequest(fixture.Server.Requests.Last(), true, path);
        Assert.Equal(AgentTaskStatus.Completed, reopened.Catalog.SelectedTask!.Status);
        Assert.Empty(await fixture.Tasks.GetProjectsAsync(fixture.Token));
        await reopened.ShutdownAsync();
    }

    [Fact]
    public async Task ExplicitProjectlessPdfUsesSnapshotHistoryAndNoImplicitFileAccess()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        await EnableFilesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(
            fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var path = Path.Combine(Path.GetDirectoryName(fixture.DataRoot)!, "projectless.pdf");
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        await File.WriteAllBytesAsync(path, bytes, fixture.Token);
        picker.Paths = [path];

        await workspace.Attachments.AddFilesCommand.ExecuteAsync(null);
        var chip = Assert.Single(workspace.Attachments.Items);
        Assert.Equal("application/pdf", chip.Content.MediaType);
        Assert.Equal(WorkspaceAttachmentAuthorization.CreateSelection(path), chip.AuthorizationStamp);
        Assert.Null(chip.Thumbnail);
        await workspace.Drafts.FlushAsync();
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        AssertProjectlessPdfRequest(fixture.Server.Requests.Single(), bytes);
        Assert.Null(workspace.Catalog.SelectedTask!.ProjectId);
        Assert.Equal(ApprovalPolicy.ReadOnly, workspace.Catalog.SelectedTask.ApprovalPolicy);
        Assert.Empty(await fixture.Tasks.GetProjectsAsync(fixture.Token));

        File.Delete(path);
        workspace.ComposerText = "follow up from the stored projectless PDF";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        AssertProjectlessPdfRequest(fixture.Server.Requests.Last(), bytes);
        await workspace.ShutdownAsync();
    }

    private static async Task EnableImagesAsync(WorkspaceModelSelectionFixture fixture)
    {
        var editor = fixture.Main.Models;
        editor.EditProviderModelCommand.Execute(fixture.SelectedModel);
        editor.SupportsImages = true;
        await editor.SaveModelCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
    }

    private static async Task EnableFilesAsync(WorkspaceModelSelectionFixture fixture)
    {
        var editor = fixture.Main.Models;
        editor.EditProviderModelCommand.Execute(fixture.SelectedModel);
        editor.SupportsFiles = true;
        await editor.SaveModelCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
    }

    private static void AssertRequest(JsonElement request, bool image, string source)
    {
        Assert.Empty(request.GetProperty("tools").EnumerateArray());
        var users = request.GetProperty("input").EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() == "user").ToArray();
        var first = users[0].GetProperty("content");
        if (image)
        {
            var block = Assert.Single(first.EnumerateArray());
            Assert.Equal("input_image", block.GetProperty("type").GetString());
            Assert.Equal("data:image/png;base64," + Png, block.GetProperty("image_url").GetString());
            Assert.DoesNotContain("workspace_attachments", request.GetRawText());
        }
        else
        {
            Assert.Contains("selected-file-content", first.GetString());
            Assert.Contains("workspace_attachments", first.GetString());
        }
        Assert.DoesNotContain(source, request.GetRawText());
    }

    private static void AssertProjectlessPdfRequest(JsonElement request, byte[] bytes)
    {
        Assert.Empty(request.GetProperty("tools").EnumerateArray());
        var blocks = request.GetProperty("input").EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() == "user")
            .SelectMany(message => message.GetProperty("content").ValueKind == JsonValueKind.Array
                ? message.GetProperty("content").EnumerateArray() : [])
            .ToArray();
        var block = Assert.Single(blocks,
            item => item.TryGetProperty("type", out var type) && type.GetString() == "input_file");
        Assert.Equal("projectless.pdf", block.GetProperty("filename").GetString());
        Assert.Equal("data:application/pdf;base64," + Convert.ToBase64String(bytes),
            block.GetProperty("file_data").GetString());
    }
}
