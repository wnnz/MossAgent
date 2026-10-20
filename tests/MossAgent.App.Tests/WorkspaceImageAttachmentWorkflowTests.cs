using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceImageAttachmentWorkflowTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";

    [Fact]
    public async Task ImageOnlyDraftSurvivesNavigationAndCompositionReopenThenReachesLaterTurnsAsNativeBlocks()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await EnableImagesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        Assert.True(workspace.SelectedModel!.SupportsImages);
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "selected-image.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(Png), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        Assert.Equal(Png, Assert.Single(workspace.Attachments.Items).Content.Image?.Base64Data);
        Assert.Empty(workspace.ComposerText);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        await workspace.Drafts.FlushAsync();
        workspace.Catalog.NewChatCommand.Execute(null);
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Empty(workspace.Attachments.Items);
        workspace.Catalog.SelectedProject = project;
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Single(workspace.Attachments.Items);
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);
        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        Assert.Equal(Png, Assert.Single(reopened.Attachments.Items).Content.Image?.Base64Data);
        Assert.Empty(reopened.ComposerText);
        Assert.True(reopened.PrimaryActionCommand.CanExecute(null));
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !reopened.IsRunning);
        AssertNativeImage(fixture.Server.Requests.Single());
        Assert.Equal(AgentTaskStatus.Completed, reopened.Catalog.SelectedTask!.Status);
        Assert.Empty(reopened.Attachments.Items);
        var persisted = await fixture.Tasks.GetMessagesAsync(reopened.Catalog.SelectedTask.Id, fixture.Token);
        Assert.Equal("image/png", Assert.Single(persisted.First(message => message.Role == MessageRole.User).Attachments!).MediaType);
        reopened.ComposerText = "follow up without selecting the image again";
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !reopened.IsRunning);
        AssertNativeImage(fixture.Server.Requests.Last());
        Assert.Equal(AgentTaskStatus.Completed, reopened.Catalog.SelectedTask.Status);
        await reopened.ShutdownAsync();
    }

    [Fact]
    public async Task SubmittedImageHistoryReopensFromProductionCompositionWithoutItsSource()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await EnableImagesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "history-image.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(Png), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "image before restart";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        AssertNativeImage(fixture.Server.Requests.Single());
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);
        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        reopened.Catalog.SelectTaskCommand.Execute(reopened.Catalog.Tasks.Single(task => task.Id == taskId));
        await fixture.WaitUntilAsync(() => reopened.Messages.Any(message => message.IsUserMessage));
        Assert.Single(reopened.Messages.First(message => message.IsUserMessage).Attachments);
        await reopened.Drafts.LoadCurrentAsync();
        reopened.ComposerText = "image history after restart";
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !reopened.IsRunning);
        AssertNativeImage(fixture.Server.Requests.Last());
        Assert.Equal(AgentTaskStatus.Completed, reopened.Catalog.SelectedTask!.Status);
        await reopened.ShutdownAsync();
    }

    [Fact]
    public async Task IncompatibleModelDisablesSendingWithoutDiscardingTheImageOrTextAndSwitchingBackRecovers()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await EnableImagesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var vision = workspace.SelectedModel!;
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "model-image.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(Png), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "retain this image and text";
        await workspace.Drafts.FlushAsync();
        workspace.SelectedProvider = Assert.Single(fixture.Main.Providers.Items, provider => provider.Id == fixture.AlternativeModel.ProviderId);
        workspace.SelectedModel = fixture.AlternativeModel;
        Assert.False(workspace.Attachments.IsModelCompatible);
        Assert.NotEmpty(workspace.Attachments.Status);
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);
        Assert.Equal("retain this image and text", workspace.ComposerText);
        Assert.Equal(Png, Assert.Single(workspace.Attachments.Items).Content.Image?.Base64Data);
        workspace.SelectedProvider = fixture.SelectedProvider;
        workspace.SelectedModel = vision;
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        AssertNativeImage(fixture.Server.Requests.Single());
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        await workspace.ShutdownAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCorruptRestoredImageBlocksRequestsUntilExplicitRemovalAndPreservesText(bool corrupt)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await EnableImagesAsync(fixture);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "unavailable-image.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(Png), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "preserve this body";
        await workspace.Drafts.FlushAsync();
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        var snapshot = Assert.Single(Directory.GetFiles(Path.Combine(fixture.DataRoot, "draft-attachments"), "*.txt", SearchOption.AllDirectories));
        if (corrupt) await File.WriteAllBytesAsync(snapshot, new byte[Convert.FromBase64String(Png).Length], fixture.Token);
        else File.Delete(snapshot);
        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        var chip = Assert.Single(reopened.Attachments.Items);
        Assert.False(chip.IsAvailable);
        Assert.Equal("image/png", chip.Content.MediaType);
        Assert.Null(chip.Content.Image);
        Assert.False(reopened.PrimaryActionCommand.CanExecute(null));
        reopened.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);
        chip.RemoveCommand.Execute(null);
        await reopened.Drafts.FlushAsync();
        Assert.Equal("preserve this body", reopened.ComposerText);
        Assert.True(reopened.PrimaryActionCommand.CanExecute(null));
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !reopened.IsRunning);
        Assert.DoesNotContain(Png, fixture.Server.Requests.Single().GetRawText());
        Assert.DoesNotContain("input_image", fixture.Server.Requests.Single().GetRawText());
        await reopened.ShutdownAsync();
    }

    private static async Task EnableImagesAsync(WorkspaceModelSelectionFixture fixture)
    {
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var editor = fixture.Main.Models;
        editor.EditProviderModelCommand.Execute(fixture.SelectedModel);
        editor.SupportsImages = true;
        await editor.SaveModelCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
    }

    private static void AssertNativeImage(JsonElement request)
    {
        Assert.Equal("selected-model", request.GetProperty("model").GetString());
        var imageTurns = request.GetProperty("input").EnumerateArray()
            .Where(message => message.GetProperty("content").ValueKind == JsonValueKind.Array).ToArray();
        var turn = Assert.Single(imageTurns);
        Assert.Equal("user", turn.GetProperty("role").GetString());
        var blocks = turn.GetProperty("content").EnumerateArray().ToArray();
        var image = Assert.Single(blocks, block => block.GetProperty("type").GetString() == "input_image");
        Assert.Equal("data:image/png;base64," + Png, image.GetProperty("image_url").GetString());
        Assert.DoesNotContain("workspace_attachments", request.GetRawText());
        Assert.DoesNotContain(blocks, block => block.TryGetProperty("text", out var text) && text.GetString()!.Contains(Png, StringComparison.Ordinal));
    }
}
