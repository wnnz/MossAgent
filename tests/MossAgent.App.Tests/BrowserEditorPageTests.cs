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
public sealed class BrowserEditorPageTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 580)]
    [InlineData(true, 580)]
    [InlineData(false, 800)]
    [InlineData(true, 800)]
    public Task MissingProxy_UsesVisibleSharedNoticeAndPreservesBoundSelection(bool dark, int width) => _session.DispatchAsync(() =>
    {
        var proxyId = Guid.NewGuid();
        var repository = new BrowserSettingsProbeRepository
        {
            Configuration = new(DefaultBrowserKind.Chrome, BrowserProfilePreference.Managed, null, null, proxyId)
        };
        using var settings = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        var pane = new BrowserSettingsPane { DataContext = settings, Width = width };
        var window = new AppWindow { Width = 980, Height = 820, Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var selector = Assert.Single(pane.GetVisualDescendants().OfType<AppComboBox>(),
                control => ReferenceEquals(control.ItemsSource, settings.Proxies));
            var notice = Assert.Single(pane.GetVisualDescendants().OfType<AppText>(), text => text.Text == settings.EditorNotice);
            var save = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(), button => Equals(button.Content, settings.SaveLabel));
            Assert.Same(settings.SelectedProxy, selector.SelectedItem);
            Assert.Equal(proxyId, settings.SelectedProxy!.Id);
            Assert.True(notice.IsEffectivelyVisible);
            Assert.True(notice.Bounds.Height > 0);
            Assert.InRange(notice.Bounds.Width, 1, width);
            Assert.False(save.IsEffectivelyEnabled);
            Assert.False(save.Command!.CanExecute(null));
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PendingSave_UpdatesSharedActionAndErrorAppearance(bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = new BrowserSettingsProbeRepository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => completion.Task;
        using var settings = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        var pane = new BrowserSettingsPane { DataContext = settings };
        var window = new AppWindow { Width = 980, Height = 820, Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        var pending = settings.SaveCommand.ExecuteAsync(null);
        try
        {
            window.UpdateLayout();
            var save = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(), button => ReferenceEquals(button.Command, settings.SaveCommand));
            var refresh = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(), button => ReferenceEquals(button.Command, settings.RefreshCommand));
            Assert.Equal("正在保存…", save.Content);
            Assert.False(save.IsEffectivelyEnabled);
            Assert.False(refresh.IsEffectivelyEnabled);
            completion.TrySetException(new IOException("synthetic-secret"));
            await pending;
            window.UpdateLayout();
            var status = Assert.Single(pane.GetVisualDescendants().OfType<AppText>(), text => text.Text == settings.Status);
            Assert.Contains("status-error", status.Classes);
            Assert.DoesNotContain("synthetic-secret", status.Text);
            Assert.True(save.IsEffectivelyEnabled);
        }
        finally { completion.TrySetResult(); await pending; window.Close(); }
    }, TestContext.Current.CancellationToken);
}
