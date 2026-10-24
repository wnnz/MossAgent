using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Attachments;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerSendPeerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task SendPeerInvokeKeepsDraftAndSurfacesBoundWarning() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => Task.FromException(new InvalidOperationException("synthetic secret")) };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "保留失败后的草稿。";
        workspace.Attachments.Items.Add(new WorkspaceAttachmentViewModel(new WorkspaceAttachmentContent(
            Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "next.txt"), "next.txt", "fixture text", 12), _ => { },
            authorizationStamp: WorkspaceAttachmentAuthorization.Create(workspace.Catalog.SelectedProject)));
        var composer = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow { Width = 800, Height = 600, Content = composer, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            var send = Assert.Single(composer.GetVisualDescendants().OfType<AppIconButton>(), button => button.Classes.Contains("composer-send"));
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(send)).Invoke();
            await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.HasSendPreparationError);
            Assert.Equal("保留失败后的草稿。", workspace.ComposerText);
            Assert.DoesNotContain("synthetic secret", workspace.SendPreparationError);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
