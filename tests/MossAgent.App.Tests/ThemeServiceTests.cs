using System.Text.Json;
using MossAgent.App.Services;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void DefaultsToSystemWhenNoSavedThemeExists()
    {
        var themeService = new ThemeService();

        Assert.False(themeService.IsDarkMode);
        Assert.Equal(ThemePreference.System, themeService.Preference);
    }

    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Dark)]
    [InlineData(ThemePreference.Light)]
    public void SelectedThemeIsPersisted(ThemePreference preference)
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-theme-test", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppDataPaths(root);
            var store = new JsonAppearancePreferenceStore(paths);
            var service = new ThemeService(store);

            service.SetPreference(preference);

            Assert.Equal(preference, new ThemeService(store).Preference);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SavePreservesOtherSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-theme-test", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"Theme":"Light","CodeFont":"CascadiaCode","Other":{"Value":42}}""");
            var service = new ThemeService(new JsonAppearancePreferenceStore(new AppDataPaths(root)));

            service.SetPreference(ThemePreference.Dark);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("Dark", document.RootElement.GetProperty("Theme").GetString());
            Assert.Equal("CascadiaCode", document.RootElement.GetProperty("CodeFont").GetString());
            Assert.Equal(42, document.RootElement.GetProperty("Other").GetProperty("Value").GetInt32());
            Assert.Null(service.PersistenceError);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MalformedSettingsAreNotOverwrittenAndFailureIsVisible()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-theme-test", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, "not-json");
            var service = new ThemeService(new JsonAppearancePreferenceStore(new AppDataPaths(root)));
            Assert.Equal(ThemePreference.System, service.Preference);

            service.SetPreference(ThemePreference.Dark);

            Assert.Equal(ThemePreference.Dark, service.Preference);
            Assert.NotNull(service.PersistenceError);
            Assert.Equal("not-json", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
