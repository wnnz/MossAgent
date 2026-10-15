using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationOperationVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980)]
    [InlineData(true, 980)]
    [InlineData(false, 1280)]
    [InlineData(true, 1280)]
    public Task PendingSave_HasReachableBoundCancelAndPreservesEditor(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Write = (_, cancellation) => { token = cancellation; return completion.Task; } };
        using var page = AutomationSaveOwnershipTests.Page(fixture, repository);
        await AutomationSaveOwnershipTests.OpenNewAsync(page, fixture);
        var window = new AppWindow
        {
            Content = new AutomationsView { DataContext = page },
            Width = width,
            Height = 640,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        var pending = page.SaveCommand.ExecuteAsync(null);
        try
        {
            window.UpdateLayout();
            var dialog = Assert.Single(window.GetVisualDescendants().OfType<AppSettingsDialog>(), control => control.IsOpen);
            Assert.Single(dialog.GetVisualDescendants().OfType<AppText>(), control => control.IsEffectivelyVisible && control.Text == AutomationText.Get("SavingPlan"));
            var cancel = Assert.Single(dialog.GetVisualDescendants().OfType<AppActionButton>(), control => control.IsEffectivelyVisible && control.Label == AutomationText.CancelOperation);
            Assert.True(cancel.IsEnabled);
            Assert.InRange(cancel.TranslatePoint(default, window)!.Value.Y, 0, window.Height - cancel.Bounds.Height);
            var editor = Assert.Single(dialog.GetVisualDescendants().OfType<AutomationEditorView>());
            Assert.False(editor.IsEnabled);
            Assert.Same(page.Operation.CancelCommand, cancel.Command);
            cancel.Command!.Execute(cancel.CommandParameter);
            Assert.True(token.IsCancellationRequested);
            Assert.True(page.IsBusy);
        }
        finally { completion.TrySetResult(false); await pending; window.Close(); }
        Assert.True(page.Editor.IsOpen);
        Assert.False(page.IsBusy);
        Assert.Equal(AutomationText.Get("OperationCancelled"), page.Error);
        Assert.Empty(await fixture.Automations.GetAllAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);
}
