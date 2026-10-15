using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationListVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980)]
    [InlineData(true, 980)]
    [InlineData(false, 1280)]
    [InlineData(true, 1280)]
    public Task LoadingFailureAndRetry_RenderDistinctReachableStates(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations) { Read = _ => completion.Task };
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        var pending = page.ReloadAsync();
        var window = Window(page, width, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Single(window.GetVisualDescendants().OfType<AppText>(), control => control.IsEffectivelyVisible && control.Text == AutomationText.ListLoading);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<AppText>(), control => control.IsEffectivelyVisible && control.Text == AutomationText.EmptyTitle);
            completion.TrySetException(new IOException("untrusted-secret-marker"));
            await pending;
            window.UpdateLayout();
            Assert.Single(window.GetVisualDescendants().OfType<AppText>(), control => control.IsEffectivelyVisible && control.Text == AutomationText.ListLoadFailed);
            var retry = Assert.Single(window.GetVisualDescendants().OfType<AppActionButton>(), control => control.IsEffectivelyVisible && control.Label == AutomationText.Retry);
            Assert.True(retry.IsEnabled);
            Assert.InRange(retry.TranslatePoint(default, window)!.Value.Y, 0, window.Height - retry.Bounds.Height);
            repository.Read = _ => Task.FromResult<IReadOnlyList<AutomationSchedule>>([]);
            await page.RefreshCommand.ExecuteAsync(null);
            window.UpdateLayout();
            Assert.Single(window.GetVisualDescendants().OfType<AppText>(), control => control.IsEffectivelyVisible && control.Text == AutomationText.EmptyTitle);
            Assert.False(retry.IsEffectivelyVisible);
        }
        finally { completion.TrySetResult([]); await pending; window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EditorReadFailure_OffersRetryWithoutLosingItsDraft(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await page.CreateCommand.ExecuteAsync(null);
        page.Editor.Name = "不能丢失的编辑草稿";
        repository.Read = _ => throw new IOException("untrusted-secret-marker");
        await page.ReloadAsync();
        var window = Window(page, 980, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            var dialog = Assert.Single(window.GetVisualDescendants().OfType<AppSettingsDialog>(), control => control.IsOpen);
            var retry = Assert.Single(dialog.GetVisualDescendants().OfType<AppActionButton>(), control => control.IsEffectivelyVisible && control.Label == AutomationText.Retry);
            Assert.True(retry.IsEnabled);
            Assert.Single(dialog.GetVisualDescendants().OfType<AppText>(), control => control.IsEffectivelyVisible && control.Text == page.List.LoadError);
            Assert.False(page.SaveCommand.CanExecute(null));
            repository.Read = _ => Task.FromResult<IReadOnlyList<AutomationSchedule>>([]);
            await page.RefreshCommand.ExecuteAsync(null);
            Assert.True(page.Editor.IsOpen);
            Assert.Equal("不能丢失的编辑草稿", page.Editor.Name);
            Assert.True(page.SaveCommand.CanExecute(null));
            Assert.Equal(0, repository.WriteCount);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow Window(AutomationViewModel page, int width, bool dark) => new()
    {
        Content = new AutomationsView { DataContext = page },
        Width = width,
        Height = 640,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };
}
