using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class MainWindowEventLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ThemeNotification_UpdatesAllBoundAppearanceState() => RunAsync((window, theme) =>
    {
        var changed = new List<string?>();
        window.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        theme.PersistenceError = "fixture-save-error";

        theme.SetPreference(MossAgent.Application.Settings.ThemePreference.Dark);

        Assert.True(window.IsDarkMode);
        Assert.Same(window.Palette.Dark, window.ActivePalette);
        Assert.True(window.IsDarkThemeSelected);
        Assert.False(window.IsLightThemeSelected);
        Assert.False(window.IsSystemThemeSelected);
        Assert.True(window.HasAppearanceError);
        Assert.Equal(theme.PersistenceError, window.AppearanceError);
        Assert.Equal([nameof(window.IsDarkMode), nameof(window.ActivePalette), nameof(window.IsSystemThemeSelected),
            nameof(window.IsLightThemeSelected), nameof(window.IsDarkThemeSelected),
            nameof(window.AppearanceError), nameof(window.HasAppearanceError)], changed);
    });

    [Fact]
    public Task Dispose_UnsubscribesThemeAndRejectsAlreadyQueuedNotifications() => RunAsync((window, theme) =>
    {
        Assert.Equal(1, theme.SubscriberCount);
        var pending = theme.PendingNotification;
        Assert.NotNull(pending);
        window.Dispose();
        var changed = new List<string?>();
        window.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        theme.ToggleTheme();
        pending();
        window.Dispose();

        Assert.Equal(0, theme.SubscriberCount);
        Assert.Empty(changed);
    });

    [Fact]
    public Task Dispose_RemovesProviderNavigationWithoutDisposingTheSharedPicker() => RunAsync((window, _) =>
    {
        var picker = window.Workspace.ModelPicker;
        picker.IsPickerOpen = true;
        picker.OpenProviderSettingsCommand.Execute(null);
        Assert.True(window.IsProviderSettingsSelected);
        Assert.True(window.IsSettingsOpen);
        Assert.False(picker.IsPickerOpen);
        window.IsSettingsOpen = false;
        picker.IsPickerOpen = true;
        window.Dispose();

        picker.OpenProviderSettingsCommand.Execute(null);

        Assert.False(window.IsSettingsOpen);
        Assert.True(picker.IsPickerOpen);
        Assert.True(picker.OpenProviderSettingsCommand.CanExecute(null));
    });

    private Task RunAsync(Action<MainWindowViewModel, ThemeChangeProbeService> action) => _session.DispatchAsync(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
            var theme = new ThemeChangeProbeService();
            using var window = ActivatorUtilities.CreateInstance<MainWindowViewModel>(services, theme);
            action(window, theme);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }, TestContext.Current.CancellationToken);
}
