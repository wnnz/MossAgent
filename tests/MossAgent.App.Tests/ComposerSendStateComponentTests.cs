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
public sealed class ComposerSendStateComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedPreparationKeepsDraftAndDisplaysBoundWarningWithoutMovingSendActions(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => Task.FromException(new InvalidOperationException("synthetic secret")) };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "保留失败后的草稿。";
        workspace.Attachments.Items.Add(new WorkspaceAttachmentViewModel(new WorkspaceAttachmentContent(
            Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "next.txt"), "next.txt", "fixture text", 12), _ => { }));
        var composer = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow
        {
            Width = 800,
            Height = 600,
            Content = composer,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => !workspace.IsRunning);
            window.UpdateLayout();
            var warning = Assert.Single(composer.GetVisualDescendants().OfType<AppText>(), text => text.Text == workspace.SendPreparationError);
            Assert.True(warning.IsVisible);
            Assert.True(warning.Bounds.Height > 0);
            Assert.DoesNotContain("synthetic secret", warning.Text);
            var send = Assert.Single(composer.GetVisualDescendants().OfType<AppIconButton>(), button => button.Classes.Contains("composer-send"));
            Assert.Same(workspace.PrimaryActionCommand, send.Command);
            Assert.Equal("发送消息", send.Label);
            Assert.False(send.IsAlternateIcon);
            Assert.True(send.IsEffectivelyEnabled);
            Assert.Equal("保留失败后的草稿。", Assert.Single(composer.GetVisualDescendants().OfType<AppInput>()).Text);
            Assert.Contains(composer.GetVisualDescendants().OfType<AppText>(), text => text.Text == "next.txt" && text.IsVisible);
            Assert.True(send.Bounds.Width > 0);
            Assert.True(send.Bounds.Height > 0);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
