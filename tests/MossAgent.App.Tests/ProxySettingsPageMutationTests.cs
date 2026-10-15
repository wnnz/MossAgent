using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
public sealed class ProxySettingsPageMutationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SavingTheRealPage_PublishesOnlyCatalogMembersAndClosesTheDialog(bool dark, bool creating) =>
        _session.DispatchAsync(async () =>
        {
            var repository = new ProxyMutationProbeRepository();
            var original = Profile();
            repository.Proxies.Add(original);
            using var settings = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
            await settings.RefreshCommand.ExecuteAsync(null);
            var pane = new ProxySettingsPane { DataContext = settings };
            var window = Window(pane, dark);
            window.Show();
            try
            {
                window.UpdateLayout();
                if (creating) settings.OpenCreateFormCommand.Execute(null);
                else settings.OpenEditFormCommand.Execute(original);
                window.UpdateLayout();
                settings.Name = creating ? "new proxy" : original.Name;
                settings.Password = "changed-synthetic-password";
                var unpublishedSelections = 0;
                settings.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(settings.SelectedItem)
                        && settings.SelectedItem is { } selected && !settings.Items.Contains(selected))
                        unpublishedSelections++;
                };

                PressSave(pane, settings);
                await settings.SaveCommand.ExecutionTask!;
                window.UpdateLayout();

                Assert.Equal(0, unpublishedSelections);
                Assert.False(settings.IsFormOpen);
                Assert.False(Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsDialog>()).IsOpen);
                Assert.Contains(creating ? "添加" : "更新", settings.Status, StringComparison.Ordinal);
                Assert.Equal(creating ? 2 : 1, repository.Proxies.Count);
                var saved = Assert.Single(repository.Proxies, proxy => proxy.Name == (creating ? "new proxy" : original.Name));
                Assert.True(saved.Password == "changed-synthetic-password");
                if (!creating) Assert.Equal(original.Id, saved.Id);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DraftEditsWhileSaving_RemainOpenAndKeepThePersistedIdentity() => _session.DispatchAsync(async () =>
    {
        var repository = new ProxyMutationProbeRepository();
        var original = Profile();
        repository.Proxies.Add(original);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = (_, token) => { entered.TrySetResult(); return release.Task.WaitAsync(token); };
        using var settings = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        await settings.RefreshCommand.ExecuteAsync(null);
        var pane = new ProxySettingsPane { DataContext = settings };
        var window = Window(pane, false);
        window.Show();
        try
        {
            window.UpdateLayout();
            settings.OpenEditFormCommand.Execute(original);
            settings.Password = "saved-synthetic-password";
            window.UpdateLayout();
            PressSave(pane, settings);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            settings.Password = "newer-synthetic-draft";
            release.SetResult();

            await settings.SaveCommand.ExecutionTask!;
            window.UpdateLayout();

            Assert.True(settings.IsFormOpen);
            Assert.Equal(original.Id, settings.SelectedItem?.Id);
            Assert.True(settings.Password == "newer-synthetic-draft");
            Assert.True(Assert.Single(repository.Proxies).Password == "saved-synthetic-password");
            Assert.Equal(SettingsText.Get("ProxyPreviousDraftSaved"), settings.Status);
        }
        finally { release.TrySetResult(); window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static ProxyProfile Profile() => new(Guid.NewGuid(), "editable proxy", ProxyProtocol.Http,
        "127.0.0.1", 8080, "synthetic-user", "synthetic-password", false, true);

    private static AppWindow Window(ProxySettingsPane pane, bool dark) => new()
    {
        Width = 980,
        Height = 700,
        Content = pane,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static void PressSave(ProxySettingsPane pane, ProxySettingsViewModel settings)
    {
        var button = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(),
            item => ReferenceEquals(item.Command, settings.SaveCommand));
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
