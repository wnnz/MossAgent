using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceComposerDraftControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ComposerShowsActionableReadFailureAndTheActualRetryPreservesInput(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new TestConversationDraftRepository
        {
            ReadHandler = (_, _) => Task.FromException<ConversationDraft?>(new IOException("synthetic-secret"))
        };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, conversationDrafts: repository);
        await workspace.LoadAsync();
        var composer = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow
        {
            Width = 760,
            Height = 600,
            Content = composer,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            var input = Assert.Single(composer.GetVisualDescendants().OfType<AppComposerInput>());
            input.Text = "重试时保留实际输入框内容";
            window.UpdateLayout();
            Assert.Equal(input.Text, workspace.ComposerText);
            var retry = Assert.Single(composer.GetVisualDescendants().OfType<AppButton>(),
                button => ReferenceEquals(button.Command, workspace.Drafts.RetryCommand));
            Assert.True(retry.IsEffectivelyVisible);
            Assert.True(retry.IsEffectivelyEnabled);
            Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
            repository.ReadHandler = null;

            await Assert.IsAssignableFrom<IAsyncRelayCommand>(retry.Command).ExecuteAsync(null);
            await workspace.Drafts.FlushAsync();
            window.UpdateLayout();

            Assert.Equal("重试时保留实际输入框内容", input.Text);
            Assert.False(retry.IsEffectivelyVisible);
            Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShutdownConflictCanBeResolvedInsideTheActualOverlayWithoutForceExit(bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = new TestConversationDraftRepository();
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        await drafts.LoadCurrentAsync();
        Assert.True(await repository.SaveAsync([new(drafts.Scope, "外部窗口内容", 0)], TestContext.Current.CancellationToken));
        drafts.Text = "本窗口未保存的内容";
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
            var keep = Assert.Single(overlay.GetVisualDescendants().OfType<AppButton>(),
                button => ReferenceEquals(button.Command, drafts.KeepLocalCommand));
            Assert.True(keep.IsEffectivelyVisible);
            Assert.True(keep.IsEffectivelyEnabled);
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(keep.Command).ExecuteAsync(null);
            await drafts.StopAsync();
            Assert.False(drafts.HasConflicts);
            Assert.Equal(drafts.Text, (await repository.GetAsync(drafts.Scope, TestContext.Current.CancellationToken))!.Text);
            Assert.True(state.HasError);
            Assert.False(drafts.CanSend);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
