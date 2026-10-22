using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceAttachmentPreviewFlowTests : IAsyncLifetime
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DraftAndHistoryChipsOpenImmutableFullImages(bool projectless) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: !projectless);
            await EnableImagesAsync(fixture);
            var picker = new WorkspaceAttachmentOperationProbe();
            using var runs = new WorkspaceRunTracker();
            using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs,
                attachmentPicker: picker, conversationDrafts: fixture.Drafts);
            await workspace.LoadAsync();
            var directory = Path.GetDirectoryName(fixture.DataRoot)!;
            var path = Path.Combine(directory, projectless ? "projectless-preview.png" : "project-preview.png");
            await File.WriteAllBytesAsync(path, Convert.FromBase64String(Png), fixture.Token);
            picker.Paths = [path];
            await workspace.Attachments.AddFilesCommand.ExecuteAsync(workspace.Catalog.SelectedProject);
            await workspace.Drafts.FlushAsync();
            File.Delete(path);
            var view = new WorkspaceChatView { DataContext = workspace };
            var window = new AppWindow { Width = 980, Height = 820, Content = view };
            window.Show();
            try
            {
                await OpenFirstImageAsync(window, view, workspace, fixture.Token);
                Assert.True(workspace.Attachments.Preview.HasImage);
                Assert.Equal(1, workspace.Attachments.Preview.Image!.PixelSize.Width);
                workspace.Attachments.Preview.Close();
                workspace.ComposerText = "immutable image history";
                workspace.PrimaryActionCommand.Execute(null);
                await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
                await WaitUntilAsync(window, () => view.GetVisualDescendants()
                    .OfType<AppAttachmentChip>().Any(chip => chip.OpenCommand is not null), fixture.Token);
                await OpenFirstImageAsync(window, view, workspace, fixture.Token);
                Assert.True(workspace.Attachments.Preview.HasImage);
                Assert.False(File.Exists(path));
                workspace.Attachments.Preview.Close();
            }
            finally
            {
                window.Close();
                await workspace.ShutdownAsync();
            }
        }, Token);

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

    private static async Task OpenFirstImageAsync(AppWindow window, WorkspaceChatView view,
        WorkspaceViewModel workspace, CancellationToken token)
    {
        var chip = Assert.Single(view.GetVisualDescendants().OfType<AppAttachmentChip>(),
            item => item.OpenCommand is not null);
        var open = chip.FindControl<AppButton>("OpenButton")!;
        Assert.True(open.Focus());
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await WaitUntilAsync(window, () =>
            workspace.Attachments.Preview.HasImage
            || workspace.Attachments.Preview.Status.Contains("无法显示", StringComparison.Ordinal), token);
        Assert.True(workspace.Attachments.Preview.IsOpen);
    }

    private static async Task WaitUntilAsync(AppWindow window, Func<bool> condition, CancellationToken token)
    {
        while (!condition())
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            await Task.Delay(15, token);
        }
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
