using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceAttachmentDraftControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShutdownCanRemoveOnlyUnavailableAttachmentsWithoutDiscardingTextOrForcingExit(bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = new TestConversationDraftRepository();
        var scope = new ConversationDraftScope(Guid.NewGuid(), null);
        var missing = new ConversationDraftAttachment(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "missing.txt"),
            "missing.txt", null, 10, new string('A', 64));
        var valid = missing with { Id = Guid.NewGuid(), DisplayPath = "valid.txt", Content = "text", ByteCount = 4 };
        await repository.SaveAsync([new(scope, "saved", 0, [missing, valid])], TestContext.Current.CancellationToken);
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        drafts.SelectContext(scope.ProjectId, null);
        await drafts.LoadCurrentAsync();
        drafts.Text = "must preserve edited text";
        await Assert.ThrowsAsync<InvalidOperationException>(drafts.StopAsync);
        var state = new AppShutdownViewModel { Drafts = drafts };
        state.Fail();
        var overlay = new AppShutdownOverlay { DataContext = state };
        var window = new AppWindow
        {
            Width = 800,
            Height = 600,
            Content = overlay,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var remove = Assert.Single(overlay.GetVisualDescendants().OfType<AppButton>(),
                button => ReferenceEquals(button.Command, drafts.RemoveUnavailableAttachmentsCommand));
            Assert.True(remove.IsEffectivelyVisible);
            Assert.True(remove.IsEffectivelyEnabled);
            Assert.Equal(drafts.RemoveUnavailableAttachmentsLabel, remove.Content);
            remove.Command!.Execute(null);
            await drafts.StopAsync();
            window.UpdateLayout();
            Assert.False(remove.IsEffectivelyVisible);
            var saved = (await repository.GetAsync(scope, TestContext.Current.CancellationToken))!;
            Assert.Equal("must preserve edited text", saved.Text);
            Assert.Equal(valid, Assert.Single(saved.AttachmentItems));
            Assert.False(drafts.CanSend);
            Assert.False(drafts.HasUnavailableAttachments);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
